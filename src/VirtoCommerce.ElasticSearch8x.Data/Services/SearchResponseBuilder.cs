using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Core.Search;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;


namespace VirtoCommerce.ElasticSearch8x.Data.Services
{
    public class SearchResponseBuilder
    {
        public virtual SearchResponse ToSearchResponse(SearchResponse<SearchDocument> response, VirtoCommerceSearchRequest request)
        {
            var result = new SearchResponse
            {
                TotalCount = response.Total,
                Documents = response.Hits.Select(ToSearchDocument).ToArray(),
                Aggregations = GetAggregations(response.Aggregations, request)
            };

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

        private IList<AggregationResponse> GetAggregations(AggregateDictionary searchResponseAggregations, VirtoCommerceSearchRequest request)
        {
            var result = new List<AggregationResponse>();

            if (request?.Aggregations != null && searchResponseAggregations != null)
            {
                //todo: parse aggregations
            }

            return result;
        }

    }
}
