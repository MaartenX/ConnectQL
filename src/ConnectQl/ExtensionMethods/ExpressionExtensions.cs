﻿// MIT License
//
// Copyright (c) 2017 Maarten van Sambeek.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

// ReSharper disable once CheckNamespace, Extension methods in namespace of extended classes.
namespace System.Linq.Expressions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using ConnectQl.AsyncEnumerables;
    using ConnectQl.Comparers;
    using ConnectQl.DataSources;
    using ConnectQl.Expressions;
    using ConnectQl.Expressions.Helpers;
    using ConnectQl.Expressions.Visitors;
    using ConnectQl.ExtensionMethods;
    using ConnectQl.Interfaces;
    using ConnectQl.Internal;
    using ConnectQl.Results;

    using JetBrains.Annotations;

    /// <summary>
    /// The expression extensions.
    /// </summary>
    public static class ExpressionExtensions
    {
        /// <summary>
        /// The <see cref="Error"/> constructor.
        /// </summary>
        private static readonly ConstructorInfo ErrorConstructor = typeof(Error).GetTypeInfo().DeclaredConstructors.First();

        /// <summary>
        /// The expression comparer.
        /// </summary>
        private static readonly ExpressionComparer ExpressionComparer = new ExpressionComparer();

        /// <summary>
        /// The task unwrap method.
        /// </summary>
        private static readonly MethodInfo TaskUnwrapMethod = typeof(TaskExtensions).GetRuntimeMethods().First(m => m.Name == "Unwrap" && m.IsGenericMethodDefinition);

        /// <summary>
        /// The catch errors.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        public static Expression CatchErrors(this Expression expression)
        {
            var e = Expression.Parameter(typeof(Exception), "e");

            return Expression.TryCatch(expression, Expression.Catch(e, Expression.Convert(Expression.New(ExpressionExtensions.ErrorConstructor, e), typeof(object))));
        }

        /// <summary>
        /// Tries to evaluate the expression if it is a constant. If an error occurs, returns an <see cref="Error"/> object.
        /// </summary>
        /// <param name="expression">
        /// The expression to evaluate.
        /// </param>
        /// <returns>
        /// When the expression contains field references or context references, the original expression, otherwise the result
        ///     of the evaluation or an <see cref="Error"/> object.
        /// </returns>
        public static Expression EvaluateAsValue(this Expression expression)
        {
            var canEvaluate = true;

            new GenericVisitor().Default(e =>
                {
                    if (e.NodeType == ExpressionType.Extension)
                    {
                        canEvaluate = false;
                    }
                }).Visit(expression);

            if (!canEvaluate)
            {
                return expression;
            }

            try
            {
                return Expression.Constant(expression.Eval());
            }
            catch (Exception e)
            {
                return Expression.Constant(new Error(e));
            }
        }

        /// <summary>
        /// Returns the expression without the expressions to remove.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <param name="expressionsToRemove">
        /// The expressions to remove.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        public static Expression Except(this Expression expression, params Expression[] expressionsToRemove)
        {
            return expression.SplitByAndExpressions()
                .Aggregate(
                    Enumerable.Empty<Expression>(),
                    (current, expressionToRemove) => current.Except(expressionToRemove.SplitByAndExpressions(), ExpressionExtensions.ExpressionComparer))
                .DefaultIfEmpty()
                .Aggregate(Expression.AndAlso);
        }

        /// <summary>
        /// Leaves only filter parts that contain aliases specified in <paramref name="sources"/>.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="sources">
        /// The sources.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        [CanBeNull]
        public static Expression FilterByAliases(this Expression source, HashSet<string> sources)
        {
            var ors = source.SplitByOrExpressions()
                .Select(part => part.SplitByAndExpressions().Where(a => a.GetFields().All(f => sources.Contains(f.SourceAlias))).DefaultIfEmpty(Expression.Constant(true)).Aggregate(Expression.AndAlso))
                .Distinct(ExpressionComparer.Default)
                .DefaultIfEmpty()
                .ToArray();

            return ors.Any(o => object.Equals((o as ConstantExpression)?.Value, true))
                       ? null //// Since one of the OR-parts equals TRUE, we have te return everything, so we don't filter.
                       : ors.Aggregate(Expression.OrElse);
        }

        /// <summary>
        /// The get fields.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="System.Collections.IEnumerable"/>.
        /// </returns>
        [NotNull]
        public static IEnumerable<IField> GetFields(this Expression expression)
        {
            var fields = new List<IField>();

            GenericVisitor.Visit((SourceFieldExpression field) => fields.Add(new Field(field.SourceName, field.FieldName)), expression);

            return fields.Distinct();
        }

        /// <summary>
        /// Creates a function that converts a row into a value.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <typeparam name="TParam">
        /// The type of the argument the lambda will have.
        /// </typeparam>
        /// <returns>
        /// A function that takes a row and returns the value for the expression.
        /// </returns>
        public static Func<TParam, object> GetRowExpression<TParam>(this Expression expression)
        {
            var row = Expression.Parameter(typeof(TParam));

            var tasks = new List<Tuple<Expression, ParameterExpression>>();

            var filterExpression = new GenericVisitor
                                       {
                                           (SourceFieldExpression node) => node.CreateGetter(row),
                                           (FieldExpression node) => node.CreateGetter(row),
                                           (UnaryExpression e) => e.NodeType == ExpressionType.Convert ? (e.Operand as SourceFieldExpression)?.CreateGetter(row, e.Type) : null,
                                           (GenericVisitor visitor, TaskExpression t) =>
                                               {
                                                   var parameter = Expression.Parameter(t.Expression.Type);
                                                   tasks.Add(Tuple.Create(visitor.Visit(t.Expression), parameter));
                                                   return Expression.Property(parameter, nameof(Task<object>.Result));
                                               },
                                       }.Visit(expression);

            if (filterExpression.Type != typeof(object))
            {
                filterExpression = Expression.Convert(filterExpression, typeof(object));
            }

            Expression CombineTasks(Expression e, Tuple<Expression, ParameterExpression> t)
            {
                var continueWith = typeof(Task<>).MakeGenericType(t.Item1.Type.GenericTypeArguments[0]).GetGenericMethod("ContinueWith", typeof(Func<,>));

                Debug.Assert(continueWith != null, nameof(continueWith) + " != null");

                var combined = Expression.Call(t.Item1, continueWith.MakeGenericMethod(e.Type), Expression.Lambda(e, t.Item2));
                return e.Type.IsConstructedGenericType && e.Type.GetGenericTypeDefinition() == typeof(Task<>) ? Expression.Call(null, ExpressionExtensions.TaskUnwrapMethod.MakeGenericMethod(e.Type.GenericTypeArguments[0]), combined) : combined;
            }

            //// Combine tasks into a chain of ContinueWith's.
            filterExpression = tasks.AsEnumerable().Reverse().Aggregate(filterExpression, CombineTasks);

            if (filterExpression.Type != typeof(object))
            {
                filterExpression = Expression.Convert(filterExpression, typeof(object));
            }

            return Expression.Lambda<Func<TParam, object>>(filterExpression, row).Compile();
        }

        /// <summary>
        /// Creates a function that filters the rows based on a query.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// A function that takes a row and returns true when a row should be in the result.
        /// </returns>
        public static Func<Row, bool> GetRowFilter([CanBeNull] this Expression expression)
        {
            if (expression == null)
            {
                return null;
            }

            var row = Expression.Parameter(typeof(Row));

            var filterExpression = new GenericVisitor
                                       {
                                           (GenericVisitor visitor, UnaryExpression node) =>
                                               {
                                                   var operand = visitor.Visit(node.Operand);

                                                   if (!object.ReferenceEquals(operand, node.Operand))
                                                   {
                                                       node = Expression.MakeUnary(node.NodeType, operand, node.Type);
                                                   }

                                                   return (node.Operand as SourceFieldExpression)?.CreateGetter(row, node.Type) ??
                                                          (node.Operand as FieldExpression)?.CreateGetter(row, node.Type) ??
                                                          (Expression)node;
                                               },
                                           (SourceFieldExpression node) => node.CreateGetter(row),
                                           (FieldExpression node) => node.CreateGetter(row),
                                       }.Visit(expression);

            var result = Expression.Lambda<Func<Row, bool>>(filterExpression, row);

#if DEBUG
            return ExpressionCache.Set(result, result.Compile());
#else
            return result.Compile();
#endif
        }

        /// <summary>
        /// Replaces the parameter in the specified expression.
        /// </summary>
        /// <param name="haystack">
        /// The expression in which to look for the <paramref name="needle"/>.
        /// </param>
        /// <param name="needle">
        /// The parameter expression to replace.
        /// </param>
        /// <param name="replace">
        /// The expression to replace the parameter with.
        /// </param>
        /// <typeparam name="TExpression">
        /// The type of the expression.
        /// </typeparam>
        /// <returns>
        /// The <typeparamref name="TExpression"/>.
        /// </returns>
        public static TExpression ReplaceParameter<TExpression>(this TExpression haystack, ParameterExpression needle, Expression replace)
            where TExpression : Expression
        {
            return (TExpression)new GenericVisitor
                                    {
                                        (ParameterExpression node) => node == needle ? replace : null,
                                    }.Visit(haystack);
        }

        /// <summary>
        /// Replaces the parameter with the specified name by <paramref name="replace"/>.
        /// </summary>
        /// <param name="haystack">The lambda expression to replace the parameter in.</param>
        /// <param name="parameterName">The name of the parameter to replace.</param>
        /// <param name="replace">The expression to replace with.</param>
        /// <returns>The lambda with the replaced parameter.</returns>
        public static LambdaExpression ReplaceParameter([NotNull] this LambdaExpression haystack, string parameterName, Expression replace)
        {
            var parameter = haystack.Parameters.FirstOrDefault(p => p.Name == parameterName);

            if (parameter == null)
            {
                throw new ArgumentOutOfRangeException(nameof(parameterName), $"Cannot replace parameter {parameterName}: parameter not found.");
            }

            var parameters = replace is ParameterExpression replaceParameter
                                 ? haystack.Parameters.Select(p => p == parameter ? replaceParameter : p)
                                 : haystack.Parameters.Where(p => p != parameter);
            
            return Expression.Lambda(haystack.Body.ReplaceParameter(parameter, replace), parameters);
        }


        /// <summary>
            /// Replaces the parameters in the specified expression.
            /// </summary>
            /// <param name="haystack">
            /// The expression in which to look for the <paramref name="needles"/>.
            /// </param>
            /// <param name="needles">
            /// The parameter expressions to replace.
            /// </param>
            /// <param name="replaces">
            /// The expressions to replace the parameters with.
            /// </param>
            /// <typeparam name="TExpression">
            /// The type of the expression.
            /// </typeparam>
            /// <returns>
            /// The <typeparamref name="TExpression"/>.
            /// </returns>
            public static TExpression ReplaceParameters<TExpression>(this TExpression haystack, [NotNull] IEnumerable<ParameterExpression> needles, [NotNull] IEnumerable<Expression> replaces)
            where TExpression : Expression
        {
            return needles.Zip(replaces, Tuple.Create).Aggregate(haystack, (current, replaceAction) => current.ReplaceParameter(replaceAction.Item1, replaceAction.Item2));
        }

        /// <summary>
        /// Rewrites <see cref="TaskExpression"/>s to an async expression (if needed).           a.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        public static LambdaExpression RewriteTasksToAsyncExpression([NotNull] this LambdaExpression expression)
        {
            return Expression.Lambda(expression.Body.RewriteTasksToAsyncExpression(), expression.Parameters);
        }

        /// <summary>
        /// Rewrites <see cref="TaskExpression"/>s to an async expression (if needed).
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        public static Expression RewriteTasksToAsyncExpression(this Expression expression)
        {
            if (expression is LambdaExpression lambdaExpression)
            {
                return Expression.Lambda(ExpressionExtensions.RewriteTasksToAsyncExpression(lambdaExpression.Body), lambdaExpression.Parameters);
            }

            var tasks = new List<Tuple<Expression, ParameterExpression>>();

            Expression CombineTasks(Expression e, Tuple<Expression, ParameterExpression> t)
            {
                var continueWith = typeof(Task<>).MakeGenericType(t.Item1.Type.GenericTypeArguments[0]).GetGenericMethod(nameof(Task.ContinueWith), typeof(Func<,>), typeof(TaskScheduler));

                Debug.Assert(continueWith != null, nameof(continueWith) + " != null");

                var combined = e.Type != typeof(void)
                                   ? Expression.Call(t.Item1, continueWith.MakeGenericMethod(e.Type), Expression.Lambda(e, t.Item2), Expression.Constant(TaskScheduler.Default))
                                   : Expression.Call(t.Item1, continueWith.MakeGenericMethod(typeof(bool)), Expression.Lambda(Expression.Block(e, Expression.Constant(true)), t.Item2), Expression.Constant(TaskScheduler.Default));

                return e.Type.IsConstructedGenericType && e.Type.GetGenericTypeDefinition() == typeof(Task<>) ? Expression.Call(null, ExpressionExtensions.TaskUnwrapMethod.MakeGenericMethod(e.Type.GenericTypeArguments[0]), combined) : combined;
            }

            var idx = 0;
            var replaced = new GenericVisitor
                               {
                                   (GenericVisitor visitor, TaskExpression t) =>
                                       {
                                           var parameter = Expression.Parameter(t.Expression.Type, $"result{++idx}");
                                           tasks.Add(Tuple.Create(visitor.Visit(t.Expression), parameter));
                                           return Expression.Property(parameter, nameof(Task<object>.Result));
                                       },
                               }.Visit(expression);

            return tasks.AsEnumerable().Reverse().Aggregate(replaced, CombineTasks);
        }

        /// <summary>
        /// Evaluates all variables and function calls on constants.
        /// </summary>
        /// <param name="source">
        /// The source expression.
        /// </param>
        /// <param name="context">
        /// The execution context.
        /// </param>
        /// <returns>
        /// The simplified expression.
        /// </returns>
        public static Expression Simplify(this Expression source, IExecutionContext context)
        {
            return new Simplifier().Visit(GenericVisitor.Visit((ExecutionContextExpression e) => Expression.Constant(context), source));
        }

        /// <summary>
        /// Evaluates all variables and function calls on constants.
        /// </summary>
        /// <param name="source">
        /// The source expression.
        /// </param>
        /// <param name="context">
        /// The execution context.
        /// </param>
        /// <returns>
        /// The simplified expression.
        /// </returns>
        public static Expression SimplifyExpression(this Expression source, IExecutionContext context)
        {
            return new GenericVisitor
                       {
                           (ExecutionContextExpression e) => Expression.Constant(context),
                           (GenericVisitor v, UnaryExpression e) =>
                               {
                                   var operand = e.Operand;

                                   if (!object.ReferenceEquals(operand, e.Operand))
                                   {
                                       e = Expression.MakeUnary(e.NodeType, e.Operand, e.Type);
                                   }

                                   if (e.Operand is ConstantExpression)
                                   {
                                       return e.EvalExpression();
                                   }

                                   return e;
                               },
                           (GenericVisitor v, BinaryExpression e) =>
                               {
                                   var left = v.Visit(e.Left);
                                   var right = v.Visit(e.Right);

                                   if (!object.ReferenceEquals(left, e.Left) || !object.ReferenceEquals(right, e.Right))
                                   {
                                       e = Expression.MakeBinary(e.NodeType, left, right);
                                   }

                                   if (e.Left is ConstantExpression && e.Right is ConstantExpression)
                                   {
                                       return e.EvalExpression();
                                   }

                                   if (e.NodeType == ExpressionType.AndAlso || e.NodeType == ExpressionType.And)
                                   {
                                       if (e.Left is ConstantExpression l)
                                       {
                                           return object.Equals(l.Value, true) ? e.Right : Expression.Constant(false);
                                       }

                                       if (e.Right is ConstantExpression r)
                                       {
                                           return object.Equals(r.Value, true) ? e.Left : Expression.Constant(false);
                                       }
                                   }

                                   if (e.NodeType == ExpressionType.OrElse || e.NodeType == ExpressionType.Or)
                                   {
                                       if (e.Left is ConstantExpression l)
                                       {
                                           return object.Equals(l.Value, false) ? e.Right : Expression.Constant(true);
                                       }

                                       if (e.Right is ConstantExpression r)
                                       {
                                           return object.Equals(r.Value, false) ? e.Left : Expression.Constant(true);
                                       }
                                   }

                                   return e;
                               },
                           (GenericVisitor v, MethodCallExpression e) =>
                               {
                                   var obj = v.Visit(e.Object);
                                   var arguments = v.Visit(e.Arguments);

                                   if (!object.ReferenceEquals(obj, e.Object) || !object.ReferenceEquals(arguments, e.Arguments))
                                   {
                                       e = Expression.Call(obj, e.Method, arguments.ToArray());
                                   }

                                   if ((e.Object == null || e.Object is ConstantExpression) && e.Arguments.All(a => a is ConstantExpression))
                                   {
                                       return e.EvalExpression();
                                   }

                                   return e;
                               },
                           (SourceFieldExpression e) => e,
                       }
                .Visit(source);
        }

        /// <summary>
        /// Evaluates all variables and function calls on constants.
        /// </summary>
        /// <param name="source">
        /// The source expression.
        /// </param>
        /// <returns>
        /// The simplified expression.
        /// </returns>
        public static Expression SimplifyRanges(this Expression source)
        {
            return new GenericVisitor
                       {
                           (RangeExpression r) => object.Equals(r.Min, false) && object.Equals(r.Max, true) ? Expression.Constant(true) : null,
                           (GenericVisitor v, BinaryExpression b) =>
                               {
                                   var left = v.Visit(b.Left);
                                   var right = v.Visit(b.Right);
                                   var result = object.ReferenceEquals(b.Left, left) && object.ReferenceEquals(b.Right, right)
                                                    ? b
                                                    : Expression.MakeBinary(b.NodeType, left, right);

                                   var binaryResult = result;
                                   if (binaryResult != null)
                                   {
                                       if (binaryResult.NodeType == ExpressionType.Or || binaryResult.NodeType == ExpressionType.OrElse)
                                       {
                                           if (binaryResult.Left is ConstantExpression l)
                                           {
                                               if (object.Equals(l.Value, false))
                                               {
                                                   return binaryResult.Right;
                                               }

                                               if (object.Equals(l.Value, true))
                                               {
                                                   return Expression.Constant(true);
                                               }
                                           }

                                           if (binaryResult.Right is ConstantExpression r)
                                           {
                                               if (object.Equals(r.Value, false))
                                               {
                                                   return binaryResult.Left;
                                               }

                                               if (object.Equals(r.Value, true))
                                               {
                                                   return Expression.Constant(true);
                                               }
                                           }
                                       }

                                       if (binaryResult.NodeType == ExpressionType.And || binaryResult.NodeType == ExpressionType.AndAlso)
                                       {
                                           if (binaryResult.Left is ConstantExpression l)
                                           {
                                               if (object.Equals(l.Value, true))
                                               {
                                                   return binaryResult.Right;
                                               }

                                               if (object.Equals(l.Value, false))
                                               {
                                                   return Expression.Constant(false);
                                               }
                                           }

                                           if (binaryResult.Right is ConstantExpression r)
                                           {
                                               if (object.Equals(r.Value, true))
                                               {
                                                   return binaryResult.Left;
                                               }

                                               if (object.Equals(r.Value, false))
                                               {
                                                   return Expression.Constant(false);
                                               }
                                           }
                                       }
                                   }

                                   return result;
                               },
                       }
                .Visit(source);
        }

        /// <summary>
        /// Splits an expression into multiple expression by And/AndAlsoexpressions.
        /// </summary>
        /// <param name="expression">
        /// The expression to split.
        /// </param>
        /// <returns>
        /// An enumerable of expressions that represent the different parts of the or-expression.
        /// </returns>
        [NotNull]
        public static IList<Expression> SplitByAndExpressions([CanBeNull] this Expression expression)
        {
            if (expression == null)
            {
                return new List<Expression>();
            }

            BinaryExpression GetAndExpression(Expression e) => e.NodeType == ExpressionType.And || e.NodeType == ExpressionType.AndAlso ? (BinaryExpression)e : null;

            var result = new List<Expression>();

            new GenericVisitor
                {
                    (BinaryExpression node) =>
                        {
                            var and = GetAndExpression(node);

                            if (and != null)
                            {
                                if (GetAndExpression(and.Left) == null)
                                {
                                    result.Add(and.Left);
                                }

                                if (GetAndExpression(and.Right) == null)
                                {
                                    result.Add(and.Right);
                                }
                            }

                            return null;
                        },
                }.Visit(expression);

            if (result.Count == 0)
            {
                result.Add(expression);
            }

            return result;
        }

        /// <summary>
        /// Splits an expression into multiple expression by Or/OrElse expressions.
        ///     When or-expressions are nested inise , they are moved up the expression tree.
        /// </summary>
        /// <param name="expression">
        /// The expression to split.
        /// </param>
        /// <returns>
        /// An enumerable of expressions that represent the different parts of the or-expression.
        /// </returns>
        [NotNull]
        public static List<Expression> SplitByOrExpressions(this Expression expression)
        {
            BinaryExpression GetOrExpression(Expression e) => e.NodeType == ExpressionType.Or || e.NodeType == ExpressionType.OrElse ? (BinaryExpression)e : null;

            Expression updated = null;

            var moveUpOrVisitor = new GenericVisitor
                                      {
                                          (NewArrayExpression node) => node,
                                          (NewExpression node) => node,
                                          (MethodCallExpression node) => node,
                                          (ConditionalExpression node) => node,
                                          (MemberExpression node) => node,
                                          (InvocationExpression node) => node,
                                          (GenericVisitor v, BinaryExpression node) =>
                                              {
                                                  if (node.NodeType == ExpressionType.Or || node.NodeType == ExpressionType.OrElse)
                                                  {
                                                      return null;
                                                  }

                                                  var leftOr = GetOrExpression(node.Left);
                                                  if (leftOr != null)
                                                  {
                                                      return Expression.OrElse(
                                                          Expression.MakeBinary(node.NodeType, v.Visit(leftOr.Left), v.Visit(node.Right)),
                                                          Expression.MakeBinary(node.NodeType, v.Visit(leftOr.Right), v.Visit(node.Right)));
                                                  }

                                                  var rightOr = GetOrExpression(node.Right);
                                                  if (rightOr != null)
                                                  {
                                                      return Expression.OrElse(
                                                          Expression.MakeBinary(node.NodeType, v.Visit(node.Left), v.Visit(rightOr.Left)),
                                                          Expression.MakeBinary(node.NodeType, v.Visit(node.Left), v.Visit(rightOr.Right)));
                                                  }

                                                  return null;
                                              },
                                          (GenericVisitor v, UnaryExpression node) =>
                                              {
                                                  if (node.NodeType != ExpressionType.Not)
                                                  {
                                                      return null;
                                                  }

                                                  var or = GetOrExpression(node.Operand);

                                                  return or == null
                                                             ? null
                                                             : Expression.AndAlso(
                                                                 Expression.Not(or.Left),
                                                                 Expression.Not(or.Right));
                                              },
                                      };

            do
            {
                expression = updated ?? expression;

                updated = moveUpOrVisitor.Visit(expression);
            }
            while (updated != expression);

            var result = new List<Expression>();

            new GenericVisitor
                {
                    (BinaryExpression node) =>
                        {
                            var or = GetOrExpression(node);

                            if (or != null)
                            {
                                if (GetOrExpression(or.Left) == null)
                                {
                                    result.Add(or.Left);
                                }

                                if (GetOrExpression(or.Right) == null)
                                {
                                    result.Add(or.Right);
                                }
                            }

                            return null;
                        },
                }.Visit(expression);

            if (result.Count == 0)
            {
                result.Add(expression);
            }

            return result;
        }

        /// <summary>
        /// Replaces all fields in the <paramref name="expressions"/> with the ranges for the fields found in
        ///     <paramref name="rows"/>.
        ///     Leaves all fields of <paramref name="ignoreAliases"/> intact.
        /// </summary>
        /// <param name="expressions">
        /// The expressions.
        /// </param>
        /// <param name="rows">
        /// The rows.
        /// </param>
        /// <param name="ignoreAliases">
        /// Aliases to ignore.
        /// </param>
        /// <returns>
        /// An array of <see cref="Expression"/>s.
        /// </returns>
        [ItemNotNull]
        public static async Task<Expression[]> ToRangedExpressionAsync(this IEnumerable<Expression> expressions, [NotNull] IAsyncReadOnlyCollection<Row> rows, HashSet<string> ignoreAliases)
        {
            expressions = expressions.ToArray();

            var rowParameter = Expression.Parameter(typeof(Row), "row");
            var fields = new List<Func<Row, object>>();
            var fieldLookup = new Dictionary<SourceFieldExpression, int>();
            var visitor = GenericVisitor.Create(
                (SourceFieldExpression field) =>
                    {
                        if (!ignoreAliases.Contains(field.SourceName))
                        {
                            fieldLookup[field] = fields.Count;
                            fields.Add(Expression.Lambda<Func<Row, object>>(Expression.Convert(field.CreateGetter(rowParameter), typeof(object)), rowParameter).Compile());
                        }
                    });

            foreach (var expression in expressions)
            {
                visitor.Visit(expression);
            }

            var fieldCount = fields.Count;
            var fieldMinimums = new object[fields.Count];
            var fieldMaximums = new object[fields.Count];
            var comparer = Comparer<object>.Default;

            await rows.ForEachAsync(row =>
                {
                    for (var i = 0; i < fieldCount; i++)
                    {
                        var value = fields[i](row);

                        if (value == null)
                        {
                            continue;
                        }

                        if (fieldMaximums[i] == null || comparer.Compare(value, fieldMaximums[i]) > 0)
                        {
                            fieldMaximums[i] = value;
                        }

                        if (fieldMinimums[i] == null || comparer.Compare(value, fieldMinimums[i]) < 0)
                        {
                            fieldMinimums[i] = value;
                        }
                    }
                }).ConfigureAwait(false);

            return expressions.Select(expression =>
                {
                    var replaced = new GenericVisitor
                                       {
                                           (SourceFieldExpression node) =>
                                               {
                                                   if (!fieldLookup.TryGetValue(node, out var index))
                                                   {
                                                       return null;
                                                   }

                                                   var min = fieldMinimums[index];
                                                   var max = fieldMaximums[index];

                                                   return object.Equals(min, max) ? (Expression)Expression.Constant(min, node.Type) : ConnectQlExpression.MakeRange(min, max, node.Type);
                                               },
                                       }.Visit(expression);

                    return ExpressionExtensions.MoveFieldsToTheLeft(ExpressionExtensions.MoveRangesUp(replaced));
                }).ToArray();
        }

        /// <summary>
        /// Checks if the <see cref="BinaryExpression"/> is a comparison.
        /// </summary>
        /// <param name="expression">The expresion to check.</param>
        /// <returns><c>true</c> if the <paramref name="expression"/> is a comparison, <c>false</c> otherwise.</returns>
        public static bool IsComparison([NotNull] this BinaryExpression expression)
        {
            var nodeType = expression.NodeType;

            return nodeType == ExpressionType.Equal || nodeType == ExpressionType.NotEqual || nodeType == ExpressionType.LessThan || nodeType == ExpressionType.LessThanOrEqual
                   || nodeType == ExpressionType.GreaterThan || nodeType == ExpressionType.GreaterThanOrEqual;
        }

        /// <summary>
        /// Swaps the operands and creates a new comparison.
        /// </summary>
        /// <param name="expression">The expression to swap operands for.</param>
        /// <returns>The new expression, with swapped operands, or <paramref name="expression"/> if they could not be swapped.</returns>
        public static Expression SwapOperandsForComparison([NotNull] this BinaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.LessThan:
                    return OperatorHelper.GenerateExpression(">=", expression.Right, expression.Left);
                case ExpressionType.LessThanOrEqual:
                    return OperatorHelper.GenerateExpression(">", expression.Right, expression.Left);
                case ExpressionType.GreaterThanOrEqual:
                    return OperatorHelper.GenerateExpression("<", expression.Right, expression.Left);
                case ExpressionType.GreaterThan:
                    return OperatorHelper.GenerateExpression("<=", expression.Right, expression.Left);
                case ExpressionType.Equal:
                    return OperatorHelper.GenerateExpression("=", expression.Right, expression.Left);
                case ExpressionType.NotEqual:
                    return OperatorHelper.GenerateExpression("<>", expression.Right, expression.Left);
            }

            return expression;
        }

        /// <summary>
        /// Checks if the expression contains the specified field.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <param name="source">
        /// Optional. The source the field should be in.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        internal static bool ContainsField(this Expression expression, [CanBeNull] DataSource source = null)
        {
            var result = false;

            GenericVisitor.Visit((SourceFieldExpression f) => result |= source?.Aliases.Contains(f.SourceName) ?? true, expression);

            return result;
        }

        /// <summary>
        /// The build range expression.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <param name="subExpressionPermutations">
        /// The sub expression permutations.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        private static Expression BuildRangeExpression(Expression expression, List<List<ConstantExpression>> subExpressionPermutations)
        {
            object[] values = null;

            if (expression is UnaryExpression unary)
            {
                values = subExpressionPermutations.Select(s => Expression.MakeUnary(unary.NodeType, s[0], unary.Type).Eval()).ToArray();
            }

            if (expression is BinaryExpression binary)
            {
                values = subExpressionPermutations.Select(s => Expression.MakeBinary(binary.NodeType, s[0], s[1]).Eval()).ToArray();
            }

            if (expression is MemberExpression member)
            {
                values = subExpressionPermutations.Select(s => Expression.MakeMemberAccess(s[0], member.Member).Eval()).ToArray();
            }

            if (expression is MethodCallExpression methodCall)
            {
                values = methodCall.Object == null
                             ? subExpressionPermutations.Select(s => Expression.Call(null, methodCall.Method, s).Eval()).ToArray()
                             : subExpressionPermutations.Select(s => Expression.Call(s[0], methodCall.Method, s.Skip(1)).Eval()).ToArray();
            }

            if (expression is ConditionalExpression)
            {
                values = subExpressionPermutations.Select(s => Expression.Condition(s[0], s[1], s[2]).Eval()).ToArray();
            }

            if (values?.Length == 2 && object.Equals(values[0], values[1]))
            {
                return Expression.Constant(values[0]);
            }

            return values == null ? null : ConnectQlExpression.MakeRange(values.Min(), values.Max(), expression.Type);
        }

        /// <summary>
        /// Checks if the expression contains (and not is) a range expression.
        /// </summary>
        /// <param name="expression">
        /// The expression to check for range expression.
        /// </param>
        /// <returns>
        /// <c>true</c> if <paramref name="expression"/> contains a range expression, <c>false</c> otherwise.
        /// </returns>
        private static bool ContainsRangeButIsNoRange(Expression expression)
        {
            var result = false;

            if (!(expression is RangeExpression))
            {
                new GenericVisitor
                    {
                        (RangeExpression r) => result = true,
                    }.Visit(expression);
            }

            return result;
        }

        /// <summary>
        /// Moves field expressions in comparisons to the left.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        private static Expression MoveFieldsToTheLeft(Expression expression)
        {
            return new GenericVisitor
                       {
                           (BinaryExpression node) =>
                               {
                                   if (!node.Right.ContainsField() || node.Left.ContainsField())
                                   {
                                       return null;
                                   }

                                   return node.SwapOperandsForComparison();
                               },
                       }.Visit(expression);
        }

        /// <summary>
        /// Takes all the ranges in the expression and tries to combine and simplify them as much as possible.
        /// </summary>
        /// <param name="expression">
        /// The expression to simplify.
        /// </param>
        /// <returns>
        /// The simplified expression.
        /// </returns>
        private static Expression MoveRangesUp(Expression expression)
        {
            var updated = expression;

            do
            {
                expression = updated;

                updated = new GenericVisitor
                              {
                                  (ConditionalExpression node) => ExpressionExtensions.MoveUpRange(node, node.Test, node.IfTrue, node.IfFalse),
                                  (BinaryExpression node) => ExpressionExtensions.MoveUpRange(node, node.Left, node.Right),
                                  (UnaryExpression node) => ExpressionExtensions.MoveUpRange(node, node.Operand),
                                  (MemberExpression node) => node.Expression == null ? null : ExpressionExtensions.MoveUpRange(node, node.Expression),
                                  (MethodCallExpression node) =>
                                      {
                                          var subExpressions = node.Object == null
                                                                   ? node.Arguments.ToArray()
                                                                   : new[]
                                                                         {
                                                                             node.Object,
                                                                         }.Concat(node.Arguments).ToArray();

                                          return ExpressionExtensions.MoveUpRange(node, subExpressions);
                                      },
                              }.Visit(expression);
            }
            while (expression != updated);

            return expression;
        }

        /// <summary>
        /// Tries to move up the range expression. When an expression has exactly one subexpression that is a range,
        ///     and none of the field expressions are field expressions, then we can create a range expression from the results of
        ///     the expression with the minimum and maximum value of the range parameter.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <param name="subExpressions">
        /// The sub expressions.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        [CanBeNull]
        private static Expression MoveUpRange(Expression expression, params Expression[] subExpressions)
        {
            if (expression.ContainsField() || expression is RangeExpression)
            {
                return null;
            }

            var subExpressionPermutations = new List<List<ConstantExpression>>
                                                {
                                                    new List<ConstantExpression>(),
                                                };

            foreach (var subExpression in subExpressions)
            {
                if (ExpressionExtensions.ContainsRangeButIsNoRange(subExpression))
                {
                    return null;
                }

                if (subExpression is RangeExpression range)
                {
                    foreach (var original in subExpressionPermutations.ToArray())
                    {
                        var clone = original.ToList();
                        subExpressionPermutations.Add(clone);

                        original.Add(Expression.Constant(range.Min));
                        clone.Add(Expression.Constant(range.Max));
                    }
                }
                else
                {
                    var subExpressionValue = subExpression.EvalExpression();

                    if (subExpressionValue != null)
                    {
                        foreach (var subExpressionPermutation in subExpressionPermutations)
                        {
                            subExpressionPermutation.Add(subExpressionValue);
                        }
                    }
                }
            }

            return subExpressionPermutations.Count == 1
                       ? null
                       : ExpressionExtensions.BuildRangeExpression(expression, subExpressionPermutations);
        }

        /// <summary>
        /// Evaluates the expression.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        private static object Eval(this Expression expression)
        {
            return expression.GetRowExpression<Row>()(null);
        }

        /// <summary>
        /// Evaluates the expression and returns it as a constant expression.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        private static ConstantExpression EvalExpression(this Expression expression)
        {
            try
            {
                return Expression.Constant(expression.Eval());
            }
            catch
            {
                return null;
            }
        }
    }
}