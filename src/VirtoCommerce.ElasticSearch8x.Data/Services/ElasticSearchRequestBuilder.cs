using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8x.Core;
using VirtoCommerce.ElasticSearch8x.Core.Services;
using VirtoCommerce.ElasticSearch8x.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using ElasticSearchSortOptions = Elastic.Clients.Elasticsearch.SortOptions;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;
using VirtoCommerceSortingField = VirtoCommerce.SearchModule.Core.Model.SortingField;

namespace VirtoCommerce.ElasticSearch8x.Data.Services
{
    public class ElasticSearchRequestBuilder : IElasticSearchRequestBuilder
    {
        readonly IElasticSearchFiltersBuilder _searchFiltersBuilder;
        readonly IElasticSearchAggregationsBuilder _searchAggregationsBuilder;

        public ElasticSearchRequestBuilder(
            IElasticSearchFiltersBuilder searchFiltersBuilder,
            IElasticSearchAggregationsBuilder searchAggregationsBuilder)
        {
            _searchFiltersBuilder = searchFiltersBuilder;
            _searchAggregationsBuilder = searchAggregationsBuilder;
        }

        public virtual ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new ElasticSearchRequest(indexName)
            {
                Query = GetQuery(request),
                PostFilter = _searchFiltersBuilder.GetFilterQuery(request?.Filter, availableFields),
                Aggregations = _searchAggregationsBuilder.GetAggregations(request?.Aggregations, availableFields),
            };

            if (request != null)
            {
                result.Sort = GetSorting(request.Sorting);
                result.From = request.Skip;
                result.Size = request.Take;
                result.TrackScores = request.Sorting?.Any(x => x.FieldName.EqualsInvariant(ModuleConstants.ScoreFieldName)) ?? false;

                if (request.IncludeFields?.Any() == true)
                {
                    result.Source = GetSourceFilters(request);
                }

                if (request.Take == 1)
                {
                    result.TrackTotalHits = new TrackHits(true);
                }
            }

            return result;
        }

        protected virtual Query GetQuery(VirtoCommerceSearchRequest request)
        {
            Query result = null;

            if (!string.IsNullOrEmpty(request?.SearchKeywords))
            {
                var keywords = request.SearchKeywords;
                var fields = request.SearchFields?.Select(x => x.ToElasticFieldName()).ToArray() ?? new[] { "_all" };

                var multiMatch = new MultiMatchQuery
                {
                    Fields = fields,
                    Query = keywords,
                    Analyzer = "standard",
                    Operator = Operator.And
                };

                if (request.IsFuzzySearch)
                {
                    multiMatch.Fuzziness = request.Fuzziness != null ? new Fuzziness(request.Fuzziness.Value) : null;
                }

                result = multiMatch;
            }

            return result;
        }

        protected virtual IList<ElasticSearchSortOptions> GetSorting(IEnumerable<VirtoCommerceSortingField> fields)
        {
            return fields?.Select(GetSortingField).ToArray();
        }

        protected virtual ElasticSearchSortOptions GetSortingField(VirtoCommerceSortingField field)
        {
            ElasticSearchSortOptions result;

            if (field is GeoDistanceSortingField geoSorting)
            {
                result = ElasticSearchSortOptions.GeoDistance(new GeoDistanceSort
                {
                    Field = field.FieldName.ToElasticFieldName(),
                    Location = new[] { geoSorting.Location.ToGeoLocation() },
                    Order = geoSorting.IsDescending ? SortOrder.Desc : SortOrder.Asc,
                });
            }
            else if (field.FieldName.EqualsInvariant(ModuleConstants.ScoreFieldName))
            {
                result = ElasticSearchSortOptions.Field(Field.ScoreField, new FieldSort
                {
                    Order = field.IsDescending ? SortOrder.Desc : SortOrder.Asc
                });
            }
            else
            {
                result = ElasticSearchSortOptions.Field(field.FieldName.ToElasticFieldName(), new FieldSort
                {
                    Order = field.IsDescending ? SortOrder.Desc : SortOrder.Asc,
                    Missing = "_last",
                    UnmappedType = FieldType.Long
                });
            }

            return result;
        }

        protected virtual SourceConfig GetSourceFilters(VirtoCommerceSearchRequest request)
        {
            return request?.IncludeFields != null
                ? new SourceConfig(new SourceFilter { Includes = request.IncludeFields.ToArray() })
                : null;
        }
    }
}
