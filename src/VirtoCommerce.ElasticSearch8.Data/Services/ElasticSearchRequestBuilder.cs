using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using ElasticSearchSortOptions = Elastic.Clients.Elasticsearch.SortOptions;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;
using VirtoCommerceSortingField = VirtoCommerce.SearchModule.Core.Model.SortingField;


namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public class ElasticSearchRequestBuilder : IElasticSearchRequestBuilder
    {
        private readonly IElasticSearchFiltersBuilder _searchFiltersBuilder;
        private readonly IElasticSearchAggregationsBuilder _searchAggregationsBuilder;
        private readonly ISettingsManager _settingsManager;

        private const int NearestNeighborMaxCandidates = 10000;

        public ElasticSearchRequestBuilder(
            IElasticSearchFiltersBuilder searchFiltersBuilder,
            IElasticSearchAggregationsBuilder searchAggregationsBuilder,
            ISettingsManager settingsManager)
        {
            _searchFiltersBuilder = searchFiltersBuilder;
            _searchAggregationsBuilder = searchAggregationsBuilder;
            _settingsManager = settingsManager;
        }

        public virtual ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new ElasticSearchRequest(indexName)
            {
                Query = GetQuery(request),
                PostFilter = _searchFiltersBuilder.GetFilterQuery(request?.Filter, availableFields),
                Aggregations = _searchAggregationsBuilder.GetAggregations(request?.Aggregations, availableFields),
                Sort = GetSorting(request?.Sorting),
                From = request?.Skip,
                Size = request?.Take,
                TrackScores = request?.Sorting?.Any(x => IsScoreField(x)),
                Source = GetSourceFilters(request?.IncludeFields),
                TrackTotalHits = new TrackHits(true),
                // Apply MinScore for Search by Keywords Only
                MinScore = !string.IsNullOrEmpty(request?.SearchKeywords) ? _settingsManager.GetMinScore() : null,
            };

            // use knn search and rank feature
            if (_settingsManager.GetSemanticSearchType() == ModuleConstants.ThirdPartyModel
                && !string.IsNullOrEmpty(request?.SearchKeywords))
            {
                var numCandidates = request.Take * 2;
                numCandidates = numCandidates <= NearestNeighborMaxCandidates ? numCandidates : NearestNeighborMaxCandidates;

                var knn = new KnnQuery
                {
                    k = request.Take,
                    NumCandidates = numCandidates,
                    Field = ModuleConstants.VectorPropertyName,
                    QueryVectorBuilder = QueryVectorBuilder.TextEmbedding(new TextEmbedding()
                    {
                        ModelId = _settingsManager.GetModelId(),
                        ModelText = request.SearchKeywords,
                    }),
                };

                result.Knn = new KnnQuery[] { knn };
            }

            return result;
        }

        protected virtual Query GetQuery(VirtoCommerceSearchRequest request)
        {
            if (string.IsNullOrEmpty(request?.SearchKeywords))
            {
                return null;
            }

            Query result;

            // basic search query 
            var multiMatchQuery = GetMultimatchKeywordSearchQuery(request);

            if (_settingsManager.GetSemanticSearchType() == ModuleConstants.ElserModel)
            {
                var textExpansionQuery = GetTextExpansionKeywordSearchQuery(request);

                // configure boost
                textExpansionQuery.Boost = _settingsManager.GetSemanticBoost();
                multiMatchQuery.Boost = _settingsManager.GetKeywordBoost();

                var queries = new Query[] { textExpansionQuery, multiMatchQuery };

                var boolQuery = new BoolQuery { Should = queries };

                result = Query.Bool(boolQuery);
            }
            else
            {
                result = multiMatchQuery;
            }

            return result;
        }

        private TextExpansionQuery GetTextExpansionKeywordSearchQuery(VirtoCommerceSearchRequest request)
        {
            var testExpansionQuery = new TextExpansionQuery(ModuleConstants.TokensPropertyName)
            {
                ModelId = _settingsManager.GetModelId(),
                ModelText = request.SearchKeywords,
            };

            return testExpansionQuery;
        }

        private static MultiMatchQuery GetMultimatchKeywordSearchQuery(VirtoCommerceSearchRequest request)
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

            return multiMatch;
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
            else if (IsScoreField(field))
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

        protected virtual bool IsScoreField(VirtoCommerceSortingField field)
        {
            return field.FieldName.EqualsInvariant(ModuleConstants.ScoreFieldName) ||
                            field.FieldName.EqualsInvariant(ModuleConstants.ElasticScoreFieldName);
        }

        protected virtual SourceConfig GetSourceFilters(IList<string> includeFields)
        {
            return includeFields != null
                ? new SourceConfig(new SourceFilter { Includes = includeFields.ToArray() })
                : null;
        }
    }
}
