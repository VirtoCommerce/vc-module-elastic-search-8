using System;
using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.ElasticSearch8x.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using ElasticSearchSortOptions = Elastic.Clients.Elasticsearch.SortOptions;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;
using VirtoCommerceSortingField = VirtoCommerce.SearchModule.Core.Model.SortingField;

namespace VirtoCommerce.ElasticSearch8x.Data.Services
{
    public class SearchRequestBuilder
    {
        protected const string Score = "score";

        public virtual ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new ElasticSearchRequest(indexName)
            {
                Query = GetQuery(request),
                PostFilter = GetFilters(request, availableFields),
                Aggregations = GetAggregations(request, availableFields),
            };

            if (request != null)
            {
                result.Sort = GetSorting(request.Sorting);
                result.From = request.Skip;
                result.Size = request.Take;
                result.TrackScores = request.Sorting?.Any(x => x.FieldName.EqualsInvariant(Score)) ?? false;

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

        #region Filters

        protected virtual Query GetFilters(VirtoCommerceSearchRequest request, IDictionary<PropertyName, IProperty> availableFields)
        {
            return GetFilterQueryRecursive(request?.Filter, availableFields);
        }

        protected virtual Query GetFilterQueryRecursive(IFilter filter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            switch (filter)
            {
                case IdsFilter idsFilter:
                    result = CreateIdsFilter(idsFilter);
                    break;

                case TermFilter termFilter:
                    result = CreateTermFilter(termFilter, availableFields);
                    break;

                case RangeFilter rangeFilter:
                    result = CreateRangeFilter(rangeFilter, availableFields);
                    break;

                case GeoDistanceFilter geoDistanceFilter:
                    result = CreateGeoDistanceFilter(geoDistanceFilter);
                    break;

                case NotFilter notFilter:
                    result = CreateNotFilter(notFilter, availableFields);
                    break;

                case AndFilter andFilter:
                    result = CreateAndFilter(andFilter, availableFields);
                    break;

                case OrFilter orFilter:
                    result = CreateOrFilter(orFilter, availableFields);
                    break;

                case WildCardTermFilter wildcardTermFilter:
                    result = CreateWildcardTermFilter(wildcardTermFilter);
                    break;
            }

            return result;
        }

        protected virtual IdsQuery CreateIdsFilter(IdsFilter idsFilter)
        {
            IdsQuery result = null;

            if (idsFilter?.Values != null)
            {
                result = new IdsQuery { Values = new Ids(idsFilter.Values.Select(id => new Id(id))) };
            }

            return result;
        }

        protected virtual TermsQuery CreateTermFilter(TermFilter termFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            var termValues = Array.Empty<FieldValue>();

            var property = availableFields.Where(x => x.Key.Name.EqualsInvariant(termFilter.FieldName)).Select(x => x.Value).FirstOrDefault();
            if (property?.Type?.EqualsInvariant(FieldType.Boolean.ToString()) == true)
            {
                termValues = termFilter.Values.Select(v => v switch
                {
                    "1" => FieldValue.True,
                    "0" => FieldValue.False,
                    _ => FieldValue.String(v.ToLowerInvariant())
                }).ToArray();
            }
            else
            {
                termValues = termFilter.Values.Select(x => FieldValue.String(x.ToLowerInvariant())).ToArray();
            }

            return new TermsQuery
            {
                Field = termFilter.FieldName.ToElasticFieldName(),
                Terms = new TermsQueryField(termValues)
            };
        }

        protected virtual Query CreateRangeFilter(RangeFilter rangeFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            var fieldName = rangeFilter.FieldName.ToElasticFieldName();
            var property = availableFields.Where(x => x.Key.Name.EqualsInvariant(rangeFilter.FieldName)).Select(x => x.Value).FirstOrDefault();

            if (property?.Type?.EqualsInvariant(FieldType.Date.ToString()) == true)
            {
                foreach (var value in rangeFilter.Values)
                {
                    result |= CreateDateRangeQuery(fieldName, value);
                }
            }
            else
            {
                foreach (var value in rangeFilter.Values)
                {
                    result |= CreateNumberRangeQuery(fieldName, value);
                }
            }

            return result;
        }

        protected virtual NumberRangeQuery CreateNumberRangeQuery(string fieldName, RangeFilterValue value)
        {
            var lower = default(double?);
            if (!string.IsNullOrEmpty(value.Lower) && double.TryParse(value.Lower, out var lowerParsed))
            {
                lower = lowerParsed;
            }

            var upper = default(double?);
            if (!string.IsNullOrEmpty(value.Upper) && double.TryParse(value.Lower, out var upperParsed))
            {
                upper = upperParsed;
            }

            var rangeQuery = new NumberRangeQuery(fieldName);

            if (value.IncludeLower)
            {
                rangeQuery.Gte = lower;
            }
            else
            {
                rangeQuery.Gt = lower;
            }

            if (value.IncludeUpper)
            {
                rangeQuery.Lte = upper;
            }
            else
            {
                rangeQuery.Lt = upper;
            }

            return rangeQuery;
        }

        public virtual DateRangeQuery CreateDateRangeQuery(string fieldName, RangeFilterValue value)
        {
            var termRangeQuery = new DateRangeQuery(fieldName);

            var lower = default(DateTime?);
            if (!string.IsNullOrEmpty(value.Lower) && DateTime.TryParse(value.Lower, out var lowerParsed))
            {
                lower = lowerParsed;
            }

            var upper = default(DateTime?);
            if (!string.IsNullOrEmpty(value.Upper) && DateTime.TryParse(value.Lower, out var upperParsed))
            {
                upper = upperParsed;
            }

            if (value.IncludeLower)
            {
                termRangeQuery.Gte = lower;
            }
            else
            {
                termRangeQuery.Gt = lower;
            }

            if (value.IncludeUpper)
            {
                termRangeQuery.Lte = upper;
            }
            else
            {
                termRangeQuery.Lt = upper;
            }

            return termRangeQuery;
        }

        protected virtual GeoDistanceQuery CreateGeoDistanceFilter(GeoDistanceFilter geoDistanceFilter)
        {
            return new GeoDistanceQuery
            {
                Field = geoDistanceFilter.FieldName.ToElasticFieldName(),
                Location = geoDistanceFilter.Location.ToGeoLocation(),
                Distance = $"{geoDistanceFilter.Distance}{DistanceUnit.Kilometers}", //example: "200km"
            };
        }

        protected virtual Query CreateNotFilter(NotFilter notFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (notFilter?.ChildFilter != null)
            {
                result = !GetFilterQueryRecursive(notFilter.ChildFilter, availableFields);
            }

            return result;
        }

        protected virtual Query CreateAndFilter(AndFilter andFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (andFilter?.ChildFilters != null)
            {
                foreach (var childQuery in andFilter.ChildFilters)
                {
                    result &= GetFilterQueryRecursive(childQuery, availableFields);
                }
            }

            return result;
        }

        protected virtual Query CreateOrFilter(OrFilter orFilter, IDictionary<PropertyName, IProperty> availableFields)
        {
            Query result = null;

            if (orFilter?.ChildFilters != null)
            {
                foreach (var childQuery in orFilter.ChildFilters)
                {
                    result |= GetFilterQueryRecursive(childQuery, availableFields);
                }
            }

            return result;
        }

        protected virtual Query CreateWildcardTermFilter(WildCardTermFilter wildcardTermFilter)
        {
            return new WildcardQuery(wildcardTermFilter.FieldName.ToElasticFieldName())
            {
                Value = wildcardTermFilter.Value
            };
        }

        #endregion

        #region Query

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
            else if (field.FieldName.EqualsInvariant(Score))
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

        #endregion

        #region Aggregations

        protected virtual AggregationDictionary GetAggregations(VirtoCommerceSearchRequest request, IDictionary<PropertyName, IProperty> availableFields)
        {
            var result = new Dictionary<string, Aggregation>();

            if (request?.Aggregations != null)
            {
                foreach (var aggregation in request.Aggregations)
                {
                    var aggregationId = aggregation.Id ?? aggregation.FieldName;
                    var fieldName = aggregation.FieldName.ToElasticFieldName();

                    if (IsRawKeywordField(fieldName, availableFields))
                    {
                        fieldName += ".raw";
                    }

                    var filter = GetFilterQueryRecursive(aggregation.Filter, availableFields);

                    if (aggregation is TermAggregationRequest termAggregationRequest)
                    {
                        AddTermAggregationRequest(result, aggregationId, fieldName, filter, termAggregationRequest);
                    }
                    else if (aggregation is RangeAggregationRequest rangeAggregationRequest)
                    {
                        AddRangeAggregationRequest(result, aggregationId, fieldName, filter, rangeAggregationRequest.Values, availableFields);
                    }
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
                var query = CreateRangeFilter(rangeFilter, availableFields);

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

        #endregion
    }
}
