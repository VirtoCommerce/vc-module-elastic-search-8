using System;
using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using ElasticSearchRequest = Elastic.Clients.Elasticsearch.SearchRequest;
using VirtoCommerceSearchRequest = VirtoCommerce.SearchModule.Core.Model.SearchRequest;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchRequestBuilder
    {
        [Obsolete("Use BuildRequest(VirtoCommerceSearchRequest, string, string, IDictionary<PropertyName, IProperty>)", DiagnosticId = "VC0009", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions")]
        ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, IDictionary<PropertyName, IProperty> availableFields);

        ElasticSearchRequest BuildRequest(VirtoCommerceSearchRequest request, string indexName, string documentType, IDictionary<PropertyName, IProperty> availableFields);
    }
}
