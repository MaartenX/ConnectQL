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

// ReSharper disable once CheckNamespace, extension methods in namespace of extended class.
namespace System.Collections.Generic
{
    using System;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// The enumerable extensions.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Aggregates an enumerable asynchronously.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="start">
        /// The start.
        /// </param>
        /// <param name="aggregate">
        /// The aggregate.
        /// </param>
        /// <typeparam name="TElement">
        /// The type of the elements.
        /// </typeparam>
        /// <typeparam name="TResult">
        /// The type of the result.
        /// </typeparam>
        /// <returns>
        /// The <see cref="Task"/>.
        /// </returns>
        public static async Task<TResult> AggregateAsync<TElement, TResult>([NotNull] this IEnumerable<TElement> source, TResult start, Func<TResult, TElement, Task<TResult>> aggregate)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery, because we cannot use an async function in linq aggregation.
            foreach (var element in source)
            {
                start = await aggregate(start, element);
            }

            return start;
        }
    }
}