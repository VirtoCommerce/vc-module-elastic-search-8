using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public class ElasticSearchAggregationsBuilder : IElasticSearchAggregationsBuilder
    {
        readonly IElasticSearchFiltersBuilder _searchFiltersBuilder;

        public ElasticSearchAggregationsBuilder(IElasticSearchFiltersBuilder searchFiltersBuilder)
        {
            _searchFiltersBuilder = searchFiltersBuilder;
        }

        public virtual Dictionary<string, Aggregation> GetAggregations(IList<AggregationRequest> aggregations, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new Dictionary<string, Aggregation>();

            if (aggregations.IsNullOrEmpty())
            {
                return result;
            }

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

            return result;
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
            if (facetSize != null)
            {
                facetSize = facetSize > 0 ? facetSize : int.MaxValue;
            }

            TermsAggregation termsAggregation = null;

            if (!string.IsNullOrEmpty(field))
            {
                termsAggregation = new TermsAggregation()
                {
                    Field = field,
                    Size = facetSize,
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
                var filterAggregation = new FiltersAggregation()
                {
                    Filters = new Buckets<Query>(new[] { query })
                };

                if (termsAggregation != null)
                {
                    var filters = new Aggregation
                    {
                        Filters = filterAggregation,
                        Aggregations = new Dictionary<string, Aggregation>
                        {
                            { aggregationId, new Aggregation { Terms = termsAggregation } }
                        }
                    };

                    container.Add(aggregationId, filters);
                }
                else
                {
                    container.Add(aggregationId, filterAggregation);
                }
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

                var filtersAggregation = new FiltersAggregation()
                {
                    Filters = new Buckets<Query>(buckets),
                };

                container.Add(aggregationValueId, filtersAggregation);
            }

            // Add stats aggregation for the field
            var aggregationQuery = new BoolQuery
            {
                Must = new List<Query> { filter }
            };

            var filterAggregation = new Aggregation
            {
                Filter = aggregationQuery
            };

            var statsAggregation = new StatsAggregation
            {
                Field = fieldName,
            };

            filterAggregation.Aggregations = new Dictionary<string, Aggregation>
            {
                { "stats", statsAggregation }
            };

            container.Add($"{aggregationId}-stats", filterAggregation);
        }
    }
}
