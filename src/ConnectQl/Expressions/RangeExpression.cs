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

namespace ConnectQl.Expressions
{
    using System;
    using System.Linq.Expressions;

    using JetBrains.Annotations;

    /// <summary>
    /// The range expression.
    /// </summary>
    internal class RangeExpression : Expression
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RangeExpression"/> class.
        /// </summary>
        /// <param name="min">
        /// The smallest item in the range.
        /// </param>
        /// <param name="max">
        /// The largest item in the range.
        /// </param>
        /// <param name="type">
        /// The type of the range.
        /// </param>
        protected internal RangeExpression(object min, object max, Type type)
        {
            this.Min = System.Convert.ChangeType(min, Nullable.GetUnderlyingType(type) ?? type);
            this.Max = System.Convert.ChangeType(max, Nullable.GetUnderlyingType(type) ?? type);
            this.Type = type;
        }

        /// <summary>
        /// Gets the type.
        /// </summary>
        [NotNull]
        public override Type Type { get; }

        /// <summary>
        /// Gets the node type of this <see cref="T:System.Linq.Expressions.Expression"/>.
        /// </summary>
        /// <returns>
        /// <see cref="ExpressionType.Extension"/>.
        /// </returns>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Gets the max.
        /// </summary>
        public object Max { get; }

        /// <summary>
        /// Gets the maximum value as constant expression.
        /// </summary>
        public ConstantExpression MaxExpression => Expression.Constant(this.Max, this.Type);

        /// <summary>
        /// Gets the min.
        /// </summary>
        public object Min { get; }

        /// <summary>
        /// Gets the minimum value as constant expression.
        /// </summary>
        public ConstantExpression MinExpression => Expression.Constant(this.Min, this.Type);

        /// <summary>
        /// Returns a textual representation of the <see cref="T:System.Linq.Expressions.Expression"/>.
        /// </summary>
        /// <returns>
        /// A textual representation of the <see cref="T:System.Linq.Expressions.Expression"/>.
        /// </returns>
        [NotNull]
        public override string ToString()
        {
            return $"RANGE({RangeExpression.Quote(this.Min)}, {RangeExpression.Quote(this.Max)})";
        }

        /// <summary>
        /// Reduces the node and then calls the visitor delegate on the reduced expression. The method throws an exception
        ///     if the node is not reducible.
        /// </summary>
        /// <returns>
        /// The expression being visited, or an expression which should replace it in the tree.
        /// </returns>
        /// <param name="visitor">
        /// An instance of <see cref="T:System.Func`2"/>.
        /// </param>
        [NotNull]
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }

        /// <summary>
        /// Quotes a value if it's a string.
        /// </summary>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        private static object Quote(object value) => value is string ? $"\"{value}\"" : value;
    }
}