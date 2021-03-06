// MIT License
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

namespace ConnectQl.Comparers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    using ConnectQl.Expressions;

    using JetBrains.Annotations;

    /// <summary>
    /// The expression comparer.
    /// </summary>
    internal class ExpressionComparer : IEqualityComparer<Expression>
    {
        /// <summary>
        /// The default.
        /// </summary>
        public static readonly ExpressionComparer Default = new ExpressionComparer();

        /// <summary>
        /// The ignore variable names.
        /// </summary>
        private readonly bool ignoreVariableNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionComparer"/> class.
        /// </summary>
        public ExpressionComparer()
            : this(true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExpressionComparer"/> class.
        /// </summary>
        /// <param name="ignoreVariableNames">
        /// The ignore variable names.
        /// </param>
        private ExpressionComparer(bool ignoreVariableNames)
        {
            this.ignoreVariableNames = ignoreVariableNames;
        }

        /// <summary>
        /// The equals.
        /// </summary>
        /// <param name="x">
        /// The x.
        /// </param>
        /// <param name="y">
        /// The y.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public bool Equals([CanBeNull] Expression x, [CanBeNull] Expression y)
        {
            return x == null && y == null || x != null && y != null && x.GetType() == y.GetType() &&
                   (

                       // Builtin expressions.
                       ExpressionComparer.Compare<BinaryExpression>(x, y, (first, second) => this.Equals(first.Left, second.Left) && this.Equals(first.Right, second.Right)) ||
                       ExpressionComparer.Compare<BlockExpression>(x, y, (first, second) => first.Expressions.SequenceEqual(second.Expressions, this) && this.Equals(first.Result, second.Result) && first.Variables.SequenceEqual(second.Variables, this)) ||
                       ExpressionComparer.Compare<ConditionalExpression>(x, y, (first, second) => this.Equals(first.Test, second.Test) && this.Equals(first.IfTrue, second.IfTrue) && this.Equals(first.IfFalse, second.IfFalse)) ||
                       ExpressionComparer.Compare<ConstantExpression>(x, y, (first, second) => object.Equals(first.Value, second.Value)) ||
                       ExpressionComparer.Compare<DefaultExpression>(x, y, (first, second) => true) ||
                       ExpressionComparer.Compare<GotoExpression>(x, y, (first, second) => object.Equals(first.Kind, second.Kind) && first.Target.Equals(second.Target) && this.Equals(first.Value, second.Value)) ||
                       ExpressionComparer.Compare<IndexExpression>(x, y, (first, second) => this.Equals(first.Object, second.Object) && first.Indexer.Equals(second.Indexer) && first.Arguments.SequenceEqual(second.Arguments, this)) ||
                       ExpressionComparer.Compare<InvocationExpression>(x, y, (first, second) => this.Equals(first.Expression, second.Expression) && first.Arguments.SequenceEqual(second.Arguments, this)) ||
                       ExpressionComparer.Compare<LabelExpression>(x, y, (first, second) => this.Equals(first.DefaultValue, second.DefaultValue) && object.Equals(first.Target, second.Target)) ||
                       ExpressionComparer.Compare<LambdaExpression>(x, y, (first, second) => this.Equals(first.Body, second.Body) && object.Equals(first.Name, second.Name) && first.Parameters.SequenceEqual(second.Parameters, this) && object.Equals(first.TailCall, second.TailCall) && object.Equals(first.ReturnType, second.ReturnType)) ||
                       ExpressionComparer.Compare<ListInitExpression>(x, y, (first, second) => this.Equals(first.NewExpression, second.NewExpression) && first.Initializers.Cast<Expression>().SequenceEqual(second.Initializers.Cast<Expression>(), this)) ||
                       ExpressionComparer.Compare<LoopExpression>(x, y, (first, second) => this.Equals(first.Body, second.Body) && object.Equals(first.BreakLabel, second.BreakLabel) && object.Equals(first.ContinueLabel, second.ContinueLabel)) ||
                       ExpressionComparer.Compare<MemberExpression>(x, y, (first, second) => this.Equals(first.Expression, second.Expression) && first.Member.Equals(second.Member)) ||
                       ExpressionComparer.Compare<MemberInitExpression>(x, y, (first, second) => this.Equals(first.NewExpression, second.NewExpression) && first.Bindings.SequenceEqual(second.Bindings)) ||
                       ExpressionComparer.Compare<MethodCallExpression>(x, y, (first, second) => this.Equals(first.Object, second.Object) && object.Equals(first.Method, second.Method) && first.Arguments.SequenceEqual(second.Arguments, this)) ||
                       ExpressionComparer.Compare<NewArrayExpression>(x, y, (first, second) => first.Expressions.SequenceEqual(second.Expressions, this)) ||
                       ExpressionComparer.Compare<ParameterExpression>(x, y, (first, second) => object.Equals(first.IsByRef, second.IsByRef) && first.Type == second.Type && (this.ignoreVariableNames || object.Equals(first.Name, second.Name))) ||
                       ExpressionComparer.Compare<RuntimeVariablesExpression>(x, y, (first, second) => first.Variables.SequenceEqual(second.Variables, this)) ||
                       ExpressionComparer.Compare<TypeBinaryExpression>(x, y, (first, second) => this.Equals(first.Expression, second.Expression) && first.TypeOperand == second.TypeOperand) ||
                       ExpressionComparer.Compare<UnaryExpression>(x, y, (first, second) => this.Equals(first.Operand, second.Operand)) ||

                       // Custom expressions.
                       ExpressionComparer.Compare<TaskExpression>(x, y, (first, second) => this.Equals(first.Expression, second.Expression)) ||
                       ExpressionComparer.Compare<ExecutionContextExpression>(x, y, (first, second) => true) ||
                       ExpressionComparer.Compare<SourceFieldExpression>(x, y, (first, second) => object.Equals(first.FieldName, second.FieldName) && object.Equals(first.SourceName, second.SourceName)) ||
                       ExpressionComparer.Compare<RangeExpression>(x, y, (first, second) => object.Equals(first.Min, second.Min) & object.Equals(first.Max, second.Max)));
        }

        /// <summary>
        /// The get hash code.
        /// </summary>
        /// <param name="obj">
        /// The obj.
        /// </param>
        /// <returns>
        /// The <see cref="int"/>.
        /// </returns>
        public int GetHashCode([CanBeNull] Expression obj)
        {
            return obj?.GetType().GetHashCode() ?? 0;
        }

        /// <summary>
        /// The compare.
        /// </summary>
        /// <param name="x">
        /// The x.
        /// </param>
        /// <param name="y">
        /// The y.
        /// </param>
        /// <param name="comparison">
        /// The comparison.
        /// </param>
        /// <typeparam name="T">
        /// The type of the items to compare.
        /// </typeparam>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        private static bool Compare<T>(Expression x, Expression y, Func<T, T, bool> comparison)
            where T : Expression
        {
            var first = x as T;
            var second = y as T;

            return first != null && second != null && first.NodeType == second.NodeType && first.Type == second.Type && comparison(first, second);
        }
    }
}