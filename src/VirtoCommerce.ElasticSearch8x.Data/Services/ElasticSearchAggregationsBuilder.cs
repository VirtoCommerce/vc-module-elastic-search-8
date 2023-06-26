using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8x.Core.Services;
using VirtoCommerce.ElasticSearch8x.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8x.Data.Services
{
    public class ElasticSearchAggregationsBuilder : IElasticSearchAggregationsBuilder
    {
        readonly IElasticSearchFiltersBuilder _searchFiltersBuilder;

        public ElasticSearchAggregationsBuilder(IElasticSearchFiltersBuilder searchFiltersBuilder)
        {
            _searchFiltersBuilder = searchFiltersBuilder;
        }

        public virtual AggregationDictionary GetAggregations(IList<AggregationRequest> aggregations, IDictionary<PropertyName, IProperty> availableFields)
        {
            if (aggregations.IsNullOrEmpty())
            {
                return null;
            }

            var result = new Dictionary<string, Aggregation>();

            foreach (var aggregation in aggregations)
            {
                var aggregationId = aggregation.Id ?? aggregation.FieldName;
                var fieldName = aggregation.FieldName.ToElasticFieldName();

                if (IsRawKeywordField(fieldName, availableFields))
                {
                    fieldName += ".raw";
                }

                var filter = _searchFiltersBuilder.GetFilterQuery(aggregation.Filter, availableFields);

                if (aggregation is TermAggregationRequest termAggregationRequest)
                {
                    AddTermAggregationRequest(result, aggregationId, fieldName, filter, termAggregationRequest);
                }
                else if (aggregation is RangeAggregationRequest rangeAggregationRequest)
                {
                    AddRangeAggregationRequest(result, aggregationId, fieldName, filter, rangeAggregationRequest.Values, availableFields);
                }
            }

            return result.Any() ? new AggregationDictionary(result) : null;
        }

        protected static bool IsRawKeywordField(string fieldName, IDictionary<PropertyName, IProperty> availableFields)
        {
            return availableFields
                .Any(kvp =>
                    kvp.Key.Name.EqualsInvariant(fieldName) &&
                    kvp.Value is KeywordProperty keywordProperty &&
                    keywordProperty.Fields?.TryGetProperty("raw", out _) == true);
        }

        protected virtual void AddTermAggregationRequest(IDictionary<string, Aggregation> container,
            string aggregationId,
            string field,
            Query query,
            TermAggregationRequest termAggregationRequest)
        {
            var facetSize = termAggregationRequest.Size;

            TermsAggregation termsAggregation = null;

            if (!string.IsNullOrEmpty(field))
            {
                termsAggregation = new TermsAggregation(aggregationId)
                {
                    Field = field,
                    Size = facetSize == null ? null : facetSize > 0 ? facetSize : int.MaxValue,
                };

                if (termAggregationRequest.Values?.Any() == true)
                {
                    termsAggregation.Include = new TermsInclude(termAggregationRequest.Values);
                }
            }

            if (query == null)
            {
                if (termsAggregation != null)
                {
                    container.Add(aggregationId, termsAggregation);
                }
            }
            else
            {
                var filterAggregation = new FiltersAggregation(aggregationId) { Filters = new Buckets<Query>(new[] { query }) };

                if (termsAggregation != null)
                {
                    filterAggregation.Aggregations = termsAggregation;
                }

                container.Add(aggregationId, filterAggregation);
            }
        }

        protected virtual void AddRangeAggregationRequest(Dictionary<string, Aggregation> container,
            string aggregationId,
            string fieldName,
            Query filter,
            IEnumerable<RangeAggregationRequestValue> values,
            IDictionary<PropertyName, IProperty> availableFields)
        {
            if (values == null)
            {
                return;
            }

            foreach (var value in values)
            {
                var aggregationValueId = $"{aggregationId}-{value.Id}";
                var rangeFilter = new RangeFilter
                {
                    FieldName = fieldName,
                    Values = new[] { new RangeFilterValue { Lower = value.Lower, Upper = value.Upper, IncludeLower = value.IncludeLower, IncludeUpper = value.IncludeUpper } },
                };
                var query = _searchFiltersBuilder.GetFilterQuery(rangeFilter, availableFields);

                var mustQuery = new BoolQuery
                {
                    Must = new List<Query> { filter, query }
                };

                var buckets = new List<Query> { mustQuery }.ToArray();

                var filterAggregation = new FiltersAggregation(aggregationValueId)
                {
                    Filters = new Buckets<Query>(buckets),
                };

                container.Add(aggregationValueId, filterAggregation);
            }
        }
    }
}
