using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Clients.Elasticsearch.QueryDsl;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchFiltersBuilder
    {
        Query GetFilterQuery(IFilter filter, IDictionary<PropertyName, IProperty> availableFields);
    }
}
