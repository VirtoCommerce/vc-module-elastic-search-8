using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;


namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public class ElasticSearchResponseBuilder : IElasticSearchResponseBuilder
    {
        public virtual SearchResponse ToSearchResponse(SearchResponse<SearchDocument> response, VirtoCommerceSearchRequest request)
        {
            var result = new SearchResponse();

            if (response.Total > 0)
            {
                result.TotalCount = response.Total;
                result.Documents = response.Hits.Select(ToSearchDocument).ToArray();
            }

            result.Aggregations = GetAggregations(response.Aggregations, request);

            return result;
        }

        protected virtual SearchDocument ToSearchDocument(Hit<SearchDocument> hit)
        {
            var result = new SearchDocument { Id = hit.Id };

            var fields = hit.Source ?? hit.Fields as IDictionary<string, object>;

            if (fields != null)
            {
                foreach (var field in fields)
                {
                    var name = field.Key;
                    var value = GetValue(field.Value);

                    result.Add(name, value);
                }
            }

            result.SetRelevanceScore(hit.Score);

            return result;
        }

        private static object GetValue(object value)
        {
            var result = value;

            if (result is not JsonElement jsonElement)
            {
                return result;
            }

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.Array:
                    result = jsonElement.EnumerateArray().Select(x => GetValue(x)).ToArray();
                    break;
                case JsonValueKind.Number:
                    result = jsonElement.GetDouble();
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                    result = jsonElement.GetBoolean();
                    break;
                case JsonValueKind.Null:
                    result = null;
                    break;
                default:
                    result = jsonElement.ToString();
                    break;
            }

            return result;
        }

        private static IList<AggregationResponse> GetAggregations(AggregateDictionary searchResponseAggregations, VirtoCommerceSearchRequest request)
        {
            var result = new List<AggregationResponse>();

            if (searchResponseAggregations == null || (request?.Aggregations) == null)
            {
                return result;
            }

            foreach (var aggregationRequest in request.Aggregations)
            {
                var aggregationResponse = new AggregationResponse
                {
                    Id = aggregationRequest.Id ?? aggregationRequest.FieldName,
                    Values = new List<AggregationResponseValue>()
                };

                if (aggregationRequest is TermAggregationRequest)
                {
                    AddAggregationValues(aggregationResponse, aggregationResponse.Id, aggregationResponse.Id, searchResponseAggregations);
                }
                else if (aggregationRequest is RangeAggregationRequest rangeAggregationRequest && rangeAggregationRequest.Values != null)
                {
                    AddRangeAggregationValues(searchResponseAggregations, aggregationResponse, rangeAggregationRequest);
                }

                if (aggregationResponse.Values.Any())
                {
                    result.Add(aggregationResponse);
                }

            }

            return result;
        }

        private static void AddAggregationValues(AggregationResponse aggregation, string responseKey, string valueId, AggregateDictionary searchResponseAggregations)
        {
            if (!searchResponseAggregations.TryGetValue(responseKey, out var aggregate))
            {
                return;
            }

            ConvertAggregate(aggregation, responseKey, valueId, aggregate);
        }

        private static void ConvertAggregate(AggregationResponse aggregation, string responseKey, string valueId, IAggregate aggregate)
        {
            switch (aggregate)
            {
                case StringTermsAggregate stringTermsAggregate:
                    foreach (var bucket in stringTermsAggregate.Buckets.Where(x => x.DocCount > 0))
                    {
                        var aggregationValue = new AggregationResponseValue
                        {
                            Id = bucket.Key.Value?.ToString(),
                            Count = bucket.DocCount
                        };
                        aggregation.Values.Add(aggregationValue);
                    }
                    break;
                case LongTermsAggregate longTermsAggregate:
                    foreach (var bucket in longTermsAggregate.Buckets.Where(x => x.DocCount > 0))
                    {
                        var aggregationValue = new AggregationResponseValue
                        {
                            Id = bucket.KeyAsString ?? bucket.Key.ToStringInvariant(),
                            Count = bucket.DocCount
                        };
                        aggregation.Values.Add(aggregationValue);
                    }
                    break;
                case DoubleTermsAggregate doubleTermsAggregate:
                    foreach (var bucket in doubleTermsAggregate.Buckets.Where(x => x.DocCount > 0))
                    {
                        var aggregationValue = new AggregationResponseValue
                        {
                            Id = bucket.KeyAsString ?? bucket.Key.ToStringInvariant(),
                            Count = bucket.DocCount
                        };
                        aggregation.Values.Add(aggregationValue);
                    }
                    break;

                case FiltersAggregate filtersAggregate:
                    foreach (var bucket in filtersAggregate.Buckets.Where(x => x.DocCount > 0))
                    {
                        if (bucket.Aggregations.TryGetValue(responseKey, out var bucketValues))
                        {
                            ConvertAggregate(aggregation, responseKey, valueId, bucketValues);
                        }
                        else
                        {
                            var aggregationValue = new AggregationResponseValue
                            {
                                Id = valueId,
                                Count = bucket.DocCount
                            };
                            aggregation.Values.Add(aggregationValue);
                        }
                    }

                    break;
                default:
                    return;
            }
        }

        private static void AddRangeAggregationValues(AggregateDictionary searchResponseAggregations, AggregationResponse aggregationResponse, RangeAggregationRequest rangeAggregationRequest)
        {
            foreach (var queryValueId in rangeAggregationRequest.Values.Select(x => x.Id))
            {
                var responseValueId = $"{aggregationResponse.Id}-{queryValueId}";
                AddAggregationValues(aggregationResponse, responseValueId, queryValueId, searchResponseAggregations);
            }

            TryAddAggregationStatistics(searchResponseAggregations, aggregationResponse);
        }

        private static void TryAddAggregationStatistics(AggregateDictionary searchResponseAggregations, AggregationResponse aggregationResponse)
        {
            var statsId = $"{aggregationResponse.Id}-stats";

            if (searchResponseAggregations.GetValueOrDefault(statsId) is FilterAggregate filterAggregate &&
                filterAggregate.Aggregations.GetValueOrDefault("stats") is StatsAggregate stats)
            {
                aggregationResponse.Statistics = new AggregationStatistics
                {
                    Min = stats.Min,
                    Max = stats.Max,
                };
            }
        }
    }
}
