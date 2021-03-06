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

namespace ConnectQl.AsyncEnumerables.Enumerators
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// The join enumerator.
    /// </summary>
    /// <typeparam name="TLeft">
    /// The type of the left items.
    /// </typeparam>
    /// <typeparam name="TRight">
    /// The type of the right items.
    /// </typeparam>
    /// <typeparam name="TKey">
    /// The type of the keys to join on.
    /// </typeparam>
    /// <typeparam name="TResult">
    /// The type of the result.
    /// </typeparam>
    internal class LessThanJoinEnumerator<TLeft, TRight, TKey, TResult> : AsyncEnumeratorBase<TResult>
    {
        /// <summary>
        /// The include equal.
        /// </summary>
        private readonly bool includeEqual;

        /// <summary>
        /// The is left join.
        /// </summary>
        private readonly bool isLeftJoin;

        /// <summary>
        /// The items returned.
        /// </summary>
        private long itemsReturned = -1L;

        /// <summary>
        /// The key comparer.
        /// </summary>
        private IComparer<TKey> keyComparer;

        /// <summary>
        /// The left.
        /// </summary>
        private IAsyncEnumerable<TLeft> left;

        /// <summary>
        /// The left enumerator.
        /// </summary>
        private IAsyncEnumerator<TLeft> leftEnumerator;

        /// <summary>
        /// The left key.
        /// </summary>
        private Func<TLeft, TKey> leftKey;

        /// <summary>
        /// The materialized right.
        /// </summary>
        private IAsyncReadOnlyCollection<TRight> materializedRight;

        /// <summary>
        /// The result filter.
        /// </summary>
        private Func<TLeft, TRight, bool> resultFilter;

        /// <summary>
        /// The result selector.
        /// </summary>
        private Func<TLeft, TRight, TResult> resultSelector;

        /// <summary>
        /// The right.
        /// </summary>
        private IAsyncEnumerable<TRight> right;

        /// <summary>
        /// The right enumerator.
        /// </summary>
        private IAsyncEnumerator<TRight> rightEnumerator;

        /// <summary>
        /// The right index.
        /// </summary>
        private long rightIndex;

        /// <summary>
        /// The right key.
        /// </summary>
        private Func<TRight, TKey> rightKey;

        /// <summary>
        /// The state.
        /// </summary>
        private byte state;

        /// <summary>
        /// The still enumerating.
        /// </summary>
        private bool stillEnumerating;

        /// <summary>
        /// Initializes a new instance of the <see cref="LessThanJoinEnumerator{TLeft,TRight,TKey,TResult}"/> class.
        ///     Initializes a new instance of the <see cref="JoinEnumerator{TLeft,TRight,TKey,TResult}"/> class.
        /// </summary>
        /// <param name="isLeftJoin">
        /// The is left join.
        /// </param>
        /// <param name="left">
        /// The left.
        /// </param>
        /// <param name="right">
        /// The right.
        /// </param>
        /// <param name="leftKey">
        /// The left key.
        /// </param>
        /// <param name="rightKey">
        /// The right key.
        /// </param>
        /// <param name="resultSelector">
        /// The result selector.
        /// </param>
        /// <param name="resultFilter">
        /// The result filter.
        /// </param>
        /// <param name="keyComparer">
        /// The key comparer.
        /// </param>
        /// <param name="includeEqual">
        /// <c>true</c> to include equal keys, <c>false</c> to only include items with keys greater than the left side.
        /// </param>
        public LessThanJoinEnumerator(bool isLeftJoin, IAsyncEnumerable<TLeft> left, IAsyncEnumerable<TRight> right, Func<TLeft, TKey> leftKey, Func<TRight, TKey> rightKey, Func<TLeft, TRight, TResult> resultSelector, Func<TLeft, TRight, bool> resultFilter, IComparer<TKey> keyComparer, bool includeEqual)
        {
            this.left = left;
            this.right = right;
            this.leftKey = leftKey;
            this.rightKey = rightKey;
            this.resultSelector = resultSelector;
            this.keyComparer = keyComparer;
            this.includeEqual = includeEqual;
            this.isLeftJoin = isLeftJoin;
            this.resultFilter = resultFilter;
        }

        /// <summary>
        /// Gets a value indicating whether the enumerator is synchronous.
        ///     When <c>false</c>, <see cref="IAsyncEnumerator{T}.NextBatchAsync"/> must be called when
        ///     <see cref="IAsyncEnumerator{T}.MoveNext"/> returns <c>false</c>.
        /// </summary>
        public override bool IsSynchronous => false;

        /// <summary>
        /// Disposes the <see cref="IAsyncEnumerator{T}"/>.
        /// </summary>
        /// <param name="disposing">
        /// The disposing.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.state = 4;

            this.leftEnumerator?.Dispose();
            this.rightEnumerator?.Dispose();

            this.left = null;
            this.right = null;
            this.leftKey = null;
            this.rightKey = null;
            this.resultSelector = null;
            this.keyComparer = null;
            this.leftEnumerator = null;
            this.rightEnumerator = null;
            this.materializedRight = null;
            this.resultFilter = null;
        }

        /// <summary>
        /// Gets the initial batch.
        /// </summary>
        /// <returns>
        /// The enumerator.
        /// </returns>
        [CanBeNull]
        protected override IEnumerator<TResult> InitialBatch() => null;

        /// <summary>
        /// Gets called when the next batch is needed.
        /// </summary>
        /// <returns>
        /// A task returning an <see cref="IEnumerator{T}"/> containing the next batch, of <c>null</c> when all data is
        ///     enumerated.
        /// </returns>
        protected override async Task<IEnumerator<TResult>> OnNextBatchAsync()
        {
            switch (this.state)
            {
                case 0:
                    Func<Task> setLeft =
                        async () => this.leftEnumerator = (await this.left.SortAsync((item1, item2) => this.keyComparer.Compare(this.leftKey(item1), this.leftKey(item2))).ConfigureAwait(false)).GetAsyncEnumerator();
                    Func<Task> setRight =
                        async () => this.rightEnumerator = (this.materializedRight = await this.right.SortAsync((item1, item2) => this.keyComparer.Compare(this.rightKey(item1), this.rightKey(item2))).ConfigureAwait(false)).GetAsyncEnumerator();

                    await Task.WhenAll(setLeft(), setRight()).ConfigureAwait(false);

                    return this.EnumerateItems();
                case 1: // Next batch for the left side.
                    if (!await this.leftEnumerator.NextBatchAsync().ConfigureAwait(false))
                    {
                        this.state = 3;

                        return null;
                    }

                    return this.EnumerateItems();
                case 2: // Next batch for the right side.
                    if (!await this.rightEnumerator.NextBatchAsync().ConfigureAwait(false))
                    {
                        if (this.isLeftJoin && this.itemsReturned != 0)
                        {
                            return LessThanJoinEnumerator<TLeft, TRight, TKey, TResult>.EnumerateItem(this.resultSelector(this.leftEnumerator.Current, default(TRight)));
                        }

                        this.ResetRight();
                    }

                    return this.EnumerateItems();
                case 3: // Done.
                    return null;
                case 4: // Disposed.
                    throw new ObjectDisposedException(this.GetType().ToString());
                default:
                    throw new InvalidOperationException($"Invalid state: {this.state}.");
            }
        }

        /// <summary>
        /// The enumerate item.
        /// </summary>
        /// <param name="result">
        /// The result.
        /// </param>
        /// <returns>
        /// The <see cref="IEnumerator{T}"/>.
        /// </returns>
        private static IEnumerator<TResult> EnumerateItem(TResult result)
        {
            yield return result;
        }

        /// <summary>
        /// Enumerates the items.
        /// </summary>
        /// <returns>
        /// The <see cref="IEnumerator{TResult}"/>.
        /// </returns>
        private IEnumerator<TResult> EnumerateItems()
        {
            while (this.stillEnumerating || this.leftEnumerator.MoveNext())
            {
                if (!this.stillEnumerating)
                {
                    this.itemsReturned = 0L;
                }

                var leftValue = this.leftKey(this.leftEnumerator.Current);

                while (this.rightEnumerator.MoveNext())
                {
                    var rightValue = this.rightKey(this.rightEnumerator.Current);
                    var compare = this.keyComparer.Compare(leftValue, rightValue);

                    if (this.includeEqual && compare < 0 || !this.includeEqual && compare <= 0)
                    {
                        this.rightIndex++;
                    }
                    else if (this.includeEqual && compare == 0 || !this.includeEqual && compare > 0)
                    {
                        if (this.resultFilter(this.leftEnumerator.Current, this.rightEnumerator.Current))
                        {
                            this.itemsReturned++;
                            yield return this.resultSelector(this.leftEnumerator.Current, this.rightEnumerator.Current);
                        }
                    }
                }

                if (!this.rightEnumerator.IsSynchronous)
                {
                    this.state = 2;
                    this.stillEnumerating = true;

                    yield break;
                }

                this.ResetRight();

                if (this.isLeftJoin && this.itemsReturned != 0)
                {
                    yield return this.resultSelector(this.leftEnumerator.Current, default(TRight));
                }
            }

            this.state = this.leftEnumerator.IsSynchronous ? (byte)3 : (byte)1;
        }

        /// <summary>
        /// The reset right.
        /// </summary>
        private void ResetRight()
        {
            this.rightEnumerator.Dispose();
            this.rightEnumerator = this.materializedRight.GetAsyncEnumerator(this.rightIndex);
            this.stillEnumerating = false;
        }
    }
}