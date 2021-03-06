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

namespace ConnectQl.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading.Tasks;

    using ConnectQl.AsyncEnumerables;
    using ConnectQl.Expressions;
    using ConnectQl.Expressions.Helpers;
    using ConnectQl.Expressions.Visitors;
    using ConnectQl.ExtensionMethods;
    using ConnectQl.Interfaces;
    using ConnectQl.Parser.Ast;
    using ConnectQl.Parser.Ast.Expressions;
    using ConnectQl.Parser.Ast.Sources;
    using ConnectQl.Parser.Ast.Visitors;
    using ConnectQl.Results;

    using JetBrains.Annotations;

    /// <summary>
    /// Adds an expression converter to the <see cref="INodeDataProvider"/>.
    /// </summary>
    internal static class NodeDataProviderExpressionConverter
    {
        /// <summary>
        /// Converts the <paramref name="expression"/> to an <see cref="Expression"/>.
        /// </summary>
        /// <param name="dataProvider">
        /// The data provider.
        /// </param>
        /// <param name="expression">
        /// The expression to convert.
        /// </param>
        /// <param name="allowVariables">
        /// <c>true</c> to allow blocks and variables in the expression, <c>false</c> to replace variables by their value (so they can be used in expressions).</param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        public static Expression ConvertToLinqExpression(this INodeDataProvider dataProvider, [CanBeNull] ConnectQlExpressionBase expression, bool allowVariables = true)
        {
            if (expression == null)
            {
                return null;
            }

            new Evaluator(dataProvider).Visit(expression);

            var result = NodeDataProviderExpressionConverter.CleanExpression(dataProvider.GetExpression(expression));

            if (allowVariables)
            {
                return result;
            }

            var vars = new Dictionary<ParameterExpression, Expression>();

            GenericVisitor.Visit(
                (BinaryExpression e) =>
                    {
                        if (e.NodeType == ExpressionType.Assign && e.Left is ParameterExpression parameter)
                        {
                            vars[parameter] = e.Right;
                        }

                        return null;
                    },
                result);

            result = new GenericVisitor
                         {
                             (GenericVisitor v, BlockExpression e) => v.Visit(e.Expressions.First(b => b.NodeType != ExpressionType.Assign)),
                             (ParameterExpression e) => vars.TryGetValue(e, out var value) ? value : e
                         }.Visit(result);

            return result;
        }

        /// <summary>
        /// The has functions with side effects.
        /// </summary>
        /// <param name="data">
        /// The data.
        /// </param>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <param name="hasVariableSideEffects">
        /// The has Variable Side Effects.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool HasSideEffects(this INodeDataProvider data, ConnectQlExpressionBase expression, Func<string, bool> hasVariableSideEffects)
        {
            var checker = new SideEffectsChecker(data, hasVariableSideEffects);

            checker.Visit(expression);

            return checker.HasSideEffects;
        }

        /// <summary>
        /// The has functions with side effects.
        /// </summary>
        /// <param name="data">
        /// The data.
        /// </param>
        /// <param name="source">
        /// The expression.
        /// </param>
        /// <param name="hasVariableSideEffects">
        /// The has Variable Side Effects.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool HasSideEffects(this INodeDataProvider data, SourceBase source, Func<string, bool> hasVariableSideEffects)
        {
            var checker = new SideEffectsChecker(data, hasVariableSideEffects);

            checker.Visit(source);

            return checker.HasSideEffects;
        }

        /// <summary>
        /// Cleans the expression by merging <see cref="UnaryExpression"/> and <see cref="SourceFieldExpression"/>.
        /// </summary>
        /// <param name="expression">
        /// The expression.
        /// </param>
        /// <returns>
        /// The <see cref="Expression"/>.
        /// </returns>
        private static Expression CleanExpression(Expression expression)
        {
            return new GenericVisitor
                       {
                           (GenericVisitor v, UnaryExpression e) =>
                               {
                                   var operand = v.Visit(e.Operand);

                                   if (!object.ReferenceEquals(operand, e.Operand))
                                   {
                                       e = Expression.MakeUnary(e.NodeType, e.Operand, e.Type);
                                   }

                                   if (e.Operand is SourceFieldExpression sourceFieldExpression && e.NodeType == ExpressionType.Convert)
                                   {
                                       return ConnectQlExpression.MakeSourceField(sourceFieldExpression.SourceName, sourceFieldExpression.FieldName, e.Type);
                                   }

                                   return e;
                               },
                       }.Visit(expression);
        }

        /// <summary>
        /// The evaluator.
        /// </summary>
        private class Evaluator : NodeVisitor
        {
            /// <summary>
            /// The function that marks function results.
            /// </summary>
            private static readonly MethodInfo MarkFunctionResultWithNameMethod = typeof(Evaluator).GetGenericMethod(nameof(Evaluator.MarkFunctionResultWithName), typeof(IExecutionContext), typeof(string), typeof(string), null);

            /// <summary>
            /// The variable counter.
            /// </summary>
            private static int varCounter = 0;

            /// <summary>
            /// The data.
            /// </summary>
            private readonly INodeDataProvider data;

            /// <summary>
            /// Initializes a new instance of the <see cref="Evaluator"/> class.
            /// </summary>
            /// <param name="data">
            /// The data.
            /// </param>
            public Evaluator(INodeDataProvider data)
            {
                this.data = data;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.BinaryConnectQlExpression"/> expression.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitBinarySqlExpression([NotNull] BinaryConnectQlExpression node)
            {
                var result = base.VisitBinarySqlExpression(node);

                this.data.SetExpression(node, OperatorHelper.GenerateExpression(node.Op, this.data.GetExpression(node.First), this.data.GetExpression(node.Second)));

                return result;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.ConstConnectQlExpression"/> expression.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            [NotNull]
            protected internal override Node VisitConstSqlExpression([NotNull] ConstConnectQlExpression node)
            {
                this.data.SetExpression(node, Expression.Constant(node.Value));

                return node;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.FieldReferenceConnectQlExpression"/> expression.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitFieldReferenceSqlExpression([NotNull] FieldReferenceConnectQlExpression node)
            {
                var result = base.VisitFieldReferenceSqlExpression(node);

                this.data.SetExpression(node, ConnectQlExpression.MakeSourceField(node.Source, node.Name));

                return result;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.FunctionCallConnectQlExpression"/> expression.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitFunctionCallSqlExpression(FunctionCallConnectQlExpression node)
            {
                var function = this.data.GetFunction(node).GetExpression();
                var expression = function.Body;
                var result = base.VisitFunctionCallSqlExpression(node);

                var variables = new ParameterExpression[function.Parameters.Count];
                var statements = new List<Expression>();

                for (var i = 0; i < function.Parameters.Count; i++)
                {
                    var expr = this.data.GetExpression(node.Arguments[i]);

                    if (function.Parameters[i].Type == typeof(IAsyncEnumerable<Row>) &&
                        typeof(IDataSource).GetTypeInfo().IsAssignableFrom(expr.Type.GetTypeInfo()))
                    {
                        expr = this.data.ConvertToRows(expr);
                    }

                    variables[i] = Expression.Parameter(function.Parameters[i].Type, $"var{++Evaluator.varCounter}");
                    statements.Add(Expression.Assign(variables[i], ConversionHelper.Convert(expr, function.Parameters[i].Type)));
                    expression = expression.ReplaceParameter(function.Parameters[i], variables[i]);
                }

                if (expression.Type.GetTypeInfo().ImplementedInterfaces.Contains(typeof(IDataAccess)))
                {
                    Expression<Func<string, object[], string>> getDisplayName = (name, args) =>
                        $"{name.ToUpperInvariant()}({string.Join(", ", args.Select(a => a is IAsyncEnumerable ? ((IAsyncEnumerable)a).GetElementType().Name + "[]" : a is string ? string.Concat("'", a.ToString(), "'") : a ?? "NULL"))})";

                    var displayName = getDisplayName.Body.ReplaceParameter(getDisplayName.Parameters[0], Expression.Constant(node.Name)).ReplaceParameter(getDisplayName.Parameters[1], Expression.NewArrayInit(typeof(object), variables.Select(v => Expression.Convert(v, typeof(object))).ToArray<Expression>()));

                    expression = Expression.Call(Evaluator.MarkFunctionResultWithNameMethod.MakeGenericMethod(expression.Type), ConnectQlExpression.ExecutionContext(), Expression.Constant(node.Name), displayName, expression);
                }

                statements.Add(expression);

                Expression block = Expression.Block(variables, statements);

                if (block.Type.IsConstructedGenericType && block.Type.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    block = ConnectQlExpression.MakeTask(block);
                }

                this.data.SetExpression(node, block);

                return result;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.UnaryConnectQlExpression"/>.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitUnarySqlExpression([NotNull] UnaryConnectQlExpression node)
            {
                var result = base.VisitUnarySqlExpression(node);

                this.data.SetExpression(node, OperatorHelper.GenerateExpression(node.Op, this.data.GetExpression(node.Expression)));

                return result;
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.VariableConnectQlExpression"/>.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitVariableSqlExpression([NotNull] VariableConnectQlExpression node)
            {
                var result = base.VisitVariableSqlExpression(node);
                var getVariable = typeof(IExecutionContext).GetRuntimeMethod("GetVariable", new[] { typeof(string), }).MakeGenericMethod(this.data.GetType(node).SimplifiedType);

                this.data.SetExpression(node, Expression.Call(ConnectQlExpression.ExecutionContext(), getVariable, Expression.Constant(node.Name)));

                return result;
            }

            /// <summary>
            /// Marks the function result with the specified name name.
            /// </summary>
            /// <param name="context">
            /// The context.
            /// </param>
            /// <param name="name">
            /// The name.
            /// </param>
            /// <param name="displayName">
            /// The display name.
            /// </param>
            /// <param name="function">
            /// The function.
            /// </param>
            /// <typeparam name="TFunctionResult">
            /// The type of the function result.
            /// </typeparam>
            /// <returns>
            /// The <typeparamref name="TFunctionResult"/>.
            /// </returns>
            public static TFunctionResult MarkFunctionResultWithName<TFunctionResult>(IExecutionContext context, string name, string displayName, TFunctionResult function)
                where TFunctionResult : IDataAccess
            {
                ((IInternalExecutionContext)context).SetFunctionName(function, name);
                ((IInternalExecutionContext)context).SetDisplayName(function, displayName);

                return function;
            }
        }

        /// <summary>
        /// The side effects checker.
        /// </summary>
        private class SideEffectsChecker : NodeVisitor
        {
            /// <summary>
            /// The data.
            /// </summary>
            private readonly INodeDataProvider data;

            /// <summary>
            /// The has variable side effects.
            /// </summary>
            private readonly Func<string, bool> hasVariableSideEffects;

            /// <summary>
            /// Initializes a new instance of the <see cref="SideEffectsChecker"/> class.
            /// </summary>
            /// <param name="data">
            /// The data.
            /// </param>
            /// <param name="hasVariableSideEffects">
            /// Checks if the variable has side effects.
            /// </param>
            public SideEffectsChecker(INodeDataProvider data, Func<string, bool> hasVariableSideEffects)
            {
                this.data = data;
                this.hasVariableSideEffects = hasVariableSideEffects;
            }

            /// <summary>
            /// Gets a value indicating whether the expression has side effects.
            /// </summary>
            public bool HasSideEffects { get; private set; }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.FunctionCallConnectQlExpression"/> expression.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitFunctionCallSqlExpression(FunctionCallConnectQlExpression node)
            {
                this.HasSideEffects |= this.data.GetFunction(node)?.HasSideEffects ?? true;

                return this.HasSideEffects ? node : base.VisitFunctionCallSqlExpression(node);
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Expressions.VariableConnectQlExpression"/>.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitVariableSqlExpression([NotNull] VariableConnectQlExpression node)
            {
                this.HasSideEffects |= this.hasVariableSideEffects(node.Name);

                return base.VisitVariableSqlExpression(node);
            }

            /// <summary>
            /// Visits a <see cref="ConnectQl.Parser.Ast.Sources.VariableSource"/>.
            /// </summary>
            /// <param name="node">
            /// The node.
            /// </param>
            /// <returns>
            /// The node, or a new version of the node.
            /// </returns>
            protected internal override Node VisitVariableSource([NotNull] VariableSource node)
            {
                this.HasSideEffects |= this.hasVariableSideEffects(node.Variable);

                return base.VisitVariableSource(node);
            }
        }
    }
}