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

namespace ConnectQl.DataSources
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading.Tasks;

    using ConnectQl.AsyncEnumerables;
    using ConnectQl.AsyncEnumerables.Policies;
    using ConnectQl.Expressions;
    using ConnectQl.Expressions.Visitors;
    using ConnectQl.Intellisense;
    using ConnectQl.Interfaces;
    using ConnectQl.Query;
    using ConnectQl.Results;

    using JetBrains.Annotations;

    /// <summary>
    /// The external data source.
    /// </summary>
    internal class ExternalDataSource : DataSource
    {
        /// <summary>
        /// The alias.
        /// </summary>
        private readonly string alias;

        /// <summary>
        /// The data source.
        /// </summary>
        private readonly IDataSource dataSource;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalDataSource"/> class.
        /// </summary>
        /// <param name="dataSource">
        /// The data source.
        /// </param>
        /// <param name="aliases">
        /// The aliases.
        /// </param>
        public ExternalDataSource(IDataSource dataSource, [NotNull] HashSet<string> aliases)
            : base(aliases)
        {
            this.dataSource = dataSource;
            this.alias = aliases.Single();
        }

        /// <summary>
        /// The get rows.
        /// </summary>
        /// <param name="context">
        /// The context.
        /// </param>
        /// <param name="multiPartQuery">
        /// The multi-part query.
        /// </param>
        /// <returns>
        /// The <see cref="IAsyncEnumerable{Row}"/>.
        /// </returns>
        internal override IAsyncEnumerable<Row> GetRows([NotNull] IInternalExecutionContext context, [NotNull] IMultiPartQuery multiPartQuery)
        {
            var functionName = context.GetDisplayName(this.dataSource);
            var fieldReplacer = GenericVisitor.Create((SourceFieldExpression e) => ConnectQlExpression.MakeField(e.SourceName, e.FieldName, e.Type));

            var query =
                multiPartQuery.WildcardAliases.Contains(this.alias) ?
                    new Query(
                        fieldReplacer.Visit(multiPartQuery.FilterExpression),
                        multiPartQuery.OrderByExpressions.Select(o => new OrderByExpression(fieldReplacer.Visit(o.Expression), o.Ascending)),
                        multiPartQuery.Count)
                    : new Query(
                        multiPartQuery.Fields.Where(f => f.SourceAlias == this.alias).Select(f => f.FieldName),
                        fieldReplacer.Visit(multiPartQuery.FilterExpression),
                        multiPartQuery.OrderByExpressions.Select(o => new OrderByExpression(fieldReplacer.Visit(o.Expression), o.Ascending)),
                        multiPartQuery.Count);

            Expression unsupportedFilters = null;
            IOrderByExpression[] unsupportedOrderByExpressions = null;

            // ReSharper disable once SuspiciousTypeConversion.Global, implemented in other assemblies.
            var expressionSupport = this.dataSource as IDataSourceFilterSupport;
            if (expressionSupport != null)
            {
                var parts = query.FilterExpression.SplitByAndExpressions().Cast<BinaryExpression>().Where(b => b.IsComparison()).ToArray();
                var filters = parts.ToLookup(p => expressionSupport.SupportsExpression(p), e => (Expression)e);
                var supportedFilter = filters[true].DefaultIfEmpty().Aggregate(Expression.AndAlso);

                unsupportedFilters = filters[false].DefaultIfEmpty().Aggregate(Expression.AndAlso);

                if (!query.RetrieveAllFields)
                {
                    query = new Query(query.Fields.Concat(unsupportedFilters.GetFields().Select(u => u.FieldName)).Distinct(), supportedFilter, query.OrderByExpressions, query.Count);
                }

                if (unsupportedFilters != null)
                {
                    context.Logger.Warning($"Data source {functionName} {this.alias} has unsupported filter {unsupportedFilters}. This could impact performance.");
                }
            }

            // ReSharper disable once SuspiciousTypeConversion.Global, implemented in other assemblies.
            var orderBySupport = this.dataSource as IDataSourceOrderBySupport;
            if (orderBySupport != null && !orderBySupport.SupportsOrderBy(query.OrderByExpressions))
            {
                unsupportedOrderByExpressions = query.OrderByExpressions.ToArray();

                if (!query.RetrieveAllFields)
                {
                    query = new Query(query.Fields.Concat(unsupportedOrderByExpressions.SelectMany(e => e.Expression.GetFields().Select(f => f.FieldName))).Distinct(), query.FilterExpression, null, query.Count);
                }

                context.Logger.Warning($"Data source  {functionName} {this.alias} has unsupported ORDER BY {string.Join(", ", unsupportedOrderByExpressions.Select(u => u.Expression + " " + (u.Ascending ? "ASC" : "DESC")))}. This could impact performance.");
            }

            var sourceName = functionName + (query.FilterExpression == null ? string.Empty : $" with query '{query.FilterExpression}'");

            context.Logger.Verbose($"Retrieving data from {sourceName}.");

            try
            {
                var result = this.dataSource.GetRows(context, new RowBuilder(this.alias), query).AfterLastElement(count => context.Logger.Verbose($"Retrieved {count} items from {sourceName}."));

                if (unsupportedFilters != null)
                {
                    result = result.Where(unsupportedFilters.GetRowFilter());
                }

                if (unsupportedOrderByExpressions != null)
                {
                    result.OrderBy(unsupportedOrderByExpressions);
                }

                return result;
            }
            catch (Exception e)
            {
                context.Logger.Error($"An error occurred while querying {sourceName}: {e.Message}.");

                return context.CreateEmptyAsyncEnumerable<Row>();
            }
        }

        /// <summary>
        /// Gets the descriptors for this data source.
        /// </summary>
        /// <param name="context">
        /// The execution context.
        /// </param>
        /// <returns>
        /// All data sources inside this data source.
        /// </returns>
        [ItemNotNull]
        protected internal override async Task<IEnumerable<IDataSourceDescriptor>> GetDataSourceDescriptorsAsync(IExecutionContext context)
        {
            var descriptor = this.dataSource as IDescriptableDataSource;

            return new[]
                       {
                           descriptor == null
                               ? Descriptor.DynamicDataSource(this.alias)
                               : await descriptor.GetDataSourceDescriptorAsync(this.alias, context).ConfigureAwait(false),
                       };
        }
    }
}