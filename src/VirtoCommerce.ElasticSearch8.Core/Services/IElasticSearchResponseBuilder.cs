using Elastic.Clients.Elasticsearch;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchResponseBuilder
    {
        SearchResponse ToSearchResponse(SearchResponse<SearchDocument> response, VirtoCommerceSearchRequest request);

        SuggestionResponse ToSuggestionResponse(SearchResponse<SearchDocument> response, SuggestionRequest request);
    }
}
