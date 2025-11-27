using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Core.Services
{
    public interface IElasticSearchPropertyService
    {
        IProperty CreateProperty(IndexDocumentField field);
        IProperty CreateSuggestionProperty(IndexDocumentField field);
        void ConfigureProperty(IProperty property, IndexDocumentField field);
    }
}
