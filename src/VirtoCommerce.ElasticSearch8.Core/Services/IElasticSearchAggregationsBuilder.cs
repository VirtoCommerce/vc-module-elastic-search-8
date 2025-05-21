using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchAggregationsBuilder
    {
        Dictionary<string, Aggregation> GetAggregations(IList<AggregationRequest> aggregations, IDictionary<PropertyName, IProperty> availableFields);
    }
}


