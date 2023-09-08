using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchRequestBuilder
    {
        ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields);
    }
}
