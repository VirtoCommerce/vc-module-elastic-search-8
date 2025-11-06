using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using ElasticSearchSortOptions = Elastic.Clients.Elasticsearch.SortOptions;
using RegexOptions = System.Text.RegularExpressions.RegexOptions;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;
using VirtoCommerceSortingField = VirtoCommerce.SearchModule.Core.Model.SortingField;

namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public partial class ElasticSearchRequestBuilder : IElasticSearchRequestBuilder
    {
        private readonly IElasticSearchFiltersBuilder _searchFiltersBuilder;
        private readonly IElasticSearchAggregationsBuilder _searchAggregationsBuilder;
        private readonly ISettingsManager _settingsManager;
        private readonly ILogger<ElasticSearchRequestBuilder> _logger;

        private const int NearestNeighborMaxCandidates = 10000;

        [GeneratedRegex(@"\W", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex NonWordSymbols();

        public ElasticSearchRequestBuilder(
            IElasticSearchFiltersBuilder searchFiltersBuilder,
            IElasticSearchAggregationsBuilder searchAggregationsBuilder,
            ISettingsManager settingsManager,
            ILogger<ElasticSearchRequestBuilder> logger)
        {
            _searchFiltersBuilder = searchFiltersBuilder;
            _searchAggregationsBuilder = searchAggregationsBuilder;
            _settingsManager = settingsManager;
            _logger = logger;
        }

        [Obsolete("Use BuildRequest(VirtoCommerceSearchRequest, string, string, IDictionary<PropertyName, IProperty>)", DiagnosticId = "VC0009", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        public virtual ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields)
        {
            return BuildRequest(request: request,
                indexName: indexName,
                documentType: null,
                availableFields: availableFields);
        }

        public virtual ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, string documentType, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new ElasticSearchRequest(indexName)
            {
                Query = GetQuery(request),
                PostFilter = _searchFiltersBuilder.GetFilterQuery(request?.Filter, availableFields),
                Aggregations = _searchAggregationsBuilder.GetAggregations(request?.Aggregations, availableFields),
                Sort = GetSorting(request?.Sorting),
                From = request?.Skip,
                Size = request?.Take,
                TrackScores = request?.Sorting?.Any(IsScoreField),
                Source = GetSourceFilters(request?.IncludeFields),
                TrackTotalHits = new TrackHits(true),
                // Apply MinScore for Search by Keywords Only
                MinScore = !string.IsNullOrEmpty(request?.SearchKeywords) ? _settingsManager.GetMinScore(documentType, _logger) : null,
            };

            // use knn search and rank feature
            if (_settingsManager.GetSemanticSearchType() == ModuleConstants.ThirdPartyModel
                && !string.IsNullOrEmpty(request?.SearchKeywords))
            {
                var numCandidates = request.Take * 2;
                numCandidates = numCandidates <= NearestNeighborMaxCandidates ? numCandidates : NearestNeighborMaxCandidates;

                var knn = new KnnSearch
                {
                    K = request.Take,
                    NumCandidates = numCandidates,
                    Field = ModuleConstants.VectorPropertyName,
                    QueryVectorBuilder = new QueryVectorBuilder
                    {
                        TextEmbedding = new TextEmbedding
                        {
                            ModelId = _settingsManager.GetModelId(),
                            ModelText = request.SearchKeywords,
                        },
                    },
                };

                result.Knn = new List<KnnSearch> { knn };
            }

            return result;
        }

        public ElasticSearchRequest BuildSuggestionRequest(SuggestionRequest request, string indexName, string documentType, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new ElasticSearchRequest(indexName)
            {
                Source = true,
                SourceIncludes = request.Fields.Select(x => x.ToElasticFieldName()).ToArray(),
                Suggest = new Suggester
                {
                    Suggesters = request.Fields.ToDictionary(GetSuggesterName,
                        field => new FieldSuggester
                        {
                            Prefix = request.Query,
                            Completion = new CompletionSuggester
                            {
                                Field = field.ToElasticFieldName(),
                                Size = request.Size,
                                SkipDuplicates = true,
                                Contexts = request.QueryContext?.ToDictionary<KeyValuePair<string, object>, Field, ICollection<CompletionContext>>(
                                    ctx => ctx.Key,
                                    ctx => new List<CompletionContext>{ new(Convert.ToString(ctx.Value)) }),
                            },
                        }),
                },
            };

            return result;
        }

        protected static string GetSuggesterName(string fieldName)
        {
            return $"{NonWordSymbols().Replace(fieldName, "-").ToLowerInvariant()}-suggest";
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
                var sparceVectorQuery = GetSparseVectorQuery(request);

                // configure boost
                sparceVectorQuery.Boost = _settingsManager.GetSemanticBoost();
                multiMatchQuery.Boost = _settingsManager.GetKeywordBoost();

                var multiMatchQueryWrapper = new Query { MultiMatch = multiMatchQuery };
                var sparceVectorQueryWrapper = new Query { SparseVector = sparceVectorQuery };

                var queries = new[] { sparceVectorQueryWrapper, multiMatchQueryWrapper };
                var boolQuery = new BoolQuery { Should = queries };

                result = new Query { Bool = boolQuery };
            }
            else
            {
                result = multiMatchQuery;
            }

            return result;
        }

        protected SparseVectorQuery GetSparseVectorQuery(VirtoCommerceSearchRequest request)
        {
            var sparseVectorQuery = new SparseVectorQuery(ModuleConstants.TokensPropertyName)
            {
                InferenceId = _settingsManager.GetModelId(),
                Query = request.SearchKeywords,
            };

            return sparseVectorQuery;
        }

        protected static MultiMatchQuery GetMultimatchKeywordSearchQuery(VirtoCommerceSearchRequest request)
        {
            var keywords = request.SearchKeywords;
            var fields = request.SearchFields?.Select(x => x.ToElasticFieldName()).ToArray() ?? ["_all"];

            var multiMatch = new MultiMatchQuery
            {
                Fields = fields,
                Query = keywords,
                Analyzer = "standard",
                Operator = Operator.And,
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
                result = new ElasticSearchSortOptions
                {
                    GeoDistance = new GeoDistanceSort
                    {
                        Field = field.FieldName.ToElasticFieldName(),
                        Location = [geoSorting.Location.ToGeoLocation()],
                        Order = geoSorting.IsDescending ? SortOrder.Desc : SortOrder.Asc,
                    },
                };
            }
            else if (IsScoreField(field))
            {
                result = new ElasticSearchSortOptions
                {
                    Field = new FieldSort(Field.ScoreField)
                    {
                        Order = field.IsDescending ? SortOrder.Desc : SortOrder.Asc,
                    },
                };
            }
            else
            {
                result = new ElasticSearchSortOptions
                {
                    Field = new FieldSort(field.FieldName.ToElasticFieldName())
                    {
                        Order = field.IsDescending ? SortOrder.Desc : SortOrder.Asc,
                        Missing = "_last",
                        UnmappedType = FieldType.Long,
                    },
                };
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
