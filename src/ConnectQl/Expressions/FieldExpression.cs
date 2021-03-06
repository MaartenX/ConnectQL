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

namespace ConnectQl.Expressions
{
    using System;
    using System.Linq.Expressions;
    using System.Reflection;

    using ConnectQl.ExtensionMethods;
    using ConnectQl.Interfaces;
    using ConnectQl.Results;

    using JetBrains.Annotations;

    /// <summary>
    /// The field expression.
    /// </summary>
    public sealed class FieldExpression : Expression
    {
        /// <summary>
        /// The <see cref="Row.Get{T}"/> method.
        /// </summary>
        private static readonly MethodInfo RowGetMethod = typeof(Row).GetGenericMethod(nameof(Row.Get), typeof(string));

        /// <summary>
        /// The source.
        /// </summary>
        private readonly string source;

        /// <summary>
        /// Initializes a new instance of the <see cref="FieldExpression"/> class.
        /// </summary>
        /// <param name="source">
        /// The source this field belongs to.
        /// </param>
        /// <param name="fieldName">
        /// The field name.
        /// </param>
        /// <param name="type">
        /// The type.
        /// </param>
        internal FieldExpression([NotNull] string source, [NotNull] string fieldName, [NotNull] Type type)
        {
            this.source = source;
            this.FieldName = fieldName;
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
        /// Gets the field name.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// Creates a method call that gets the value from the specified parameter.
        /// </summary>
        /// <param name="row">
        /// The parameter to get the field from.
        /// </param>
        /// <param name="type">
        /// The type to return (when omitted, the node's type will be returned).
        /// </param>
        /// <returns>
        /// The <see cref="MethodCallExpression"/>.
        /// </returns>
        public MethodCallExpression CreateGetter(ParameterExpression row, [CanBeNull] Type type = null)
            => Expression.Call(row, FieldExpression.RowGetMethod.MakeGenericMethod(type ?? this.Type), Expression.Constant($"{this.source}.{this.FieldName}"));

        /// <summary>
        /// Returns a textual representation of the <see cref="T:System.Linq.Expressions.Expression"/>.
        /// </summary>
        /// <returns>
        /// A textual representation of the <see cref="T:System.Linq.Expressions.Expression"/>.
        /// </returns>
        [NotNull]
        public override string ToString()
        {
            return $"[{this.FieldName}]";
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
    }
}