using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;
using VirtoCommerceSuggestionRequest = VirtoCommerce.SearchModule.Core.Model.SuggestionRequest;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchRequestBuilder
    {
        ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, string documentType, IDictionary<PropertyName, IProperty> availableFields);

        ElasticSearchRequest BuildSuggestionRequest(VirtoCommerceSuggestionRequest request, string indexName, string documentType, IDictionary<PropertyName, IProperty> availableFields);
    }
}
