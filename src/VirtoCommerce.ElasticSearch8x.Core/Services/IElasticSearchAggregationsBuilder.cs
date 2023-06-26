using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8x.Core.Services
{
    public interface IElasticSearchAggregationsBuilder
    {
        AggregationDictionary GetAggregations(IList<AggregationRequest> aggregations, IDictionary<PropertyName, IProperty> availableFields);
    }
}
