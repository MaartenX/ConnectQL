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

namespace ConnectQl.AsyncEnumerables.Enumerators
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// Enumerator used by the
    ///     <see cref="AsyncEnumerableExtensions.Take{T}(ConnectQl.AsyncEnumerables.IAsyncEnumerable{T},long)"/> method.
    /// </summary>
    /// <typeparam name="TSource">
    /// The type of the source elements.
    /// </typeparam>
    internal class TakeEnumerator<TSource> : AsyncEnumeratorBase<TSource>
    {
        /// <summary>
        /// The enumerator.
        /// </summary>
        private IAsyncEnumerator<TSource> asyncEnumerator;

        /// <summary>
        /// The state.
        /// </summary>
        private byte state;

        /// <summary>
        /// The skip count.
        /// </summary>
        private long takeCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="TakeEnumerator{TSource}"/> class.
        /// </summary>
        /// <param name="source">
        /// The source.
        /// </param>
        /// <param name="takeCount">
        /// The number of items to take.
        /// </param>
        public TakeEnumerator([NotNull] IAsyncEnumerable<TSource> source, long takeCount)
        {
            this.asyncEnumerator = source.GetAsyncEnumerator();
            this.takeCount = takeCount;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TakeEnumerator{TSource}"/> class.
        /// </summary>
        /// <param name="asyncEnumerator">
        /// The async enumerator.
        /// </param>
        /// <param name="takeCount">
        /// The number of items to take.
        /// </param>
        public TakeEnumerator(IAsyncEnumerator<TSource> asyncEnumerator, long takeCount)
        {
            this.asyncEnumerator = asyncEnumerator;
            this.takeCount = takeCount;
        }

        /// <summary>
        /// Gets a value indicating whether the enumerator is synchronous.
        ///     When <c>false</c>, <see cref="IAsyncEnumerator{T}.NextBatchAsync"/> must be called when
        ///     <see cref="IAsyncEnumerator{T}.MoveNext"/> returns <c>false</c>.
        /// </summary>
        public override bool IsSynchronous => this.asyncEnumerator.IsSynchronous;

        /// <summary>
        /// Resets all fields to null, and sets the state to 'disposed'.
        /// </summary>
        /// <param name="disposing">
        /// The disposing.
        /// </param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            this.state = 2;

            this.asyncEnumerator?.Dispose();
            this.asyncEnumerator = null;
        }

        /// <summary>
        /// Gets the initial batch.
        /// </summary>
        /// <returns>
        /// The enumerator.
        /// </returns>
        protected override IEnumerator<TSource> InitialBatch()
        {
            return this.EnumerateItems();
        }

        /// <summary>
        /// Gets called when the next batch is needed.
        /// </summary>
        /// <returns>
        /// A task returning an <see cref="IEnumerator{T}"/> containing the next batch, of <c>null</c> when all data is
        ///     enumerated.
        /// </returns>
        protected override async Task<IEnumerator<TSource>> OnNextBatchAsync()
        {
            switch (this.state)
            {
                case 0:
                    if (!await this.asyncEnumerator.NextBatchAsync().ConfigureAwait(false))
                    {
                        this.state = 1;

                        return null;
                    }

                    return this.EnumerateItems();
                case 1:
                    return null;
                case 2:
                    throw new ObjectDisposedException(this.GetType().ToString());
                default:
                    throw new InvalidOperationException($"Invalid state: {this.state}.");
            }
        }

        /// <summary>
        /// Enumerates the items until the <see cref="takeCount"/> is 0.
        /// </summary>
        /// <returns>
        /// The <see cref="IEnumerator{TSource}"/>.
        /// </returns>
        private IEnumerator<TSource> EnumerateItems()
        {
            while (this.takeCount > 0 && this.asyncEnumerator.MoveNext())
            {
                yield return this.asyncEnumerator.Current;
                this.takeCount--;
            }

            if (this.takeCount == 0 || this.asyncEnumerator.IsSynchronous)
            {
                this.state = 1;
            }
        }
    }
}