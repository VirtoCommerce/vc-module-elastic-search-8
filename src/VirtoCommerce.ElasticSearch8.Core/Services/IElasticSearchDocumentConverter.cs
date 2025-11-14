using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Core.Services;

public interface IElasticSearchDocumentConverter
{
    SearchDocument ToProviderDocument(string documentType, IndexDocument indexDocument, IDictionary<PropertyName, IProperty> properties);
}
