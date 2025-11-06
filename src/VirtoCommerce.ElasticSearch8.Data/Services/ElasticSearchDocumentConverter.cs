using System.Collections.Generic;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Data.Services;

public class ElasticSearchDocumentConverter(IElasticSearchPropertyService propertyService) : IElasticSearchDocumentConverter
{
    public SearchDocument ToProviderDocument(string documentType, IndexDocument indexDocument, IDictionary<PropertyName, IProperty> properties)
    {
        var document = new SearchDocument { Id = indexDocument.Id };

        var fieldsByNames = indexDocument.Fields
            .Select(field => (FieldName: GetFieldName(documentType, field), Field: field))
            .OrderBy(x => x.FieldName)
            .ToArray();

        foreach (var (fieldName, field) in fieldsByNames)
        {
            if (document.TryGetValue(fieldName, out var value))
            {
                var newValues = new List<object>();

                if (value is object[] currentValues)
                {
                    newValues.AddRange(currentValues);
                }
                else
                {
                    newValues.Add(value);
                }

                newValues.AddRange(field.Values);
                document[fieldName] = newValues.ToArray();
            }
            else
            {
                if (!properties.TryGetValue(fieldName, out var providerField))
                {
                    providerField = CreateProperty(documentType, field);
                    if (providerField != null)
                    {
                        ConfigureProperty(documentType, providerField, field);
                        properties.Add(fieldName, providerField);
                    }
                }

                value = GetFieldValue(documentType, fieldName, field, providerField);
                if (value != null)
                {
                    document.Add(fieldName, value);
                }
            }
        }

        return document;
    }

    protected virtual string GetFieldName(string documentType, IndexDocumentField field)
    {
        return field.Name.ToElasticFieldName();
    }

    protected virtual object GetFieldValue(string documentType, string fieldName, IndexDocumentField field, IProperty property)
    {
        if (fieldName == "__object")
        {
            return null;
        }

        object result;
        var isCollection = field.IsCollection || field.Values?.Count > 1;

        if (property is GeoPointProperty)
        {
            result = isCollection
                ? field.Values.OfType<GeoPoint>().Select(x => x.ToElasticValue()).ToArray()
                : (field.Value as GeoPoint)?.ToElasticValue();
        }
        else
        {
            result = isCollection ? field.Values : field.Value;
        }

        return result;
    }

    protected virtual IProperty CreateProperty(string documentType, IndexDocumentField field)
    {
        return propertyService.CreateProperty(field);
    }

    protected virtual void ConfigureProperty(string documentType, IProperty providerField, IndexDocumentField field)
    {
        propertyService.ConfigureProperty(providerField, field);
    }
}
