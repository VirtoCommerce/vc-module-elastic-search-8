using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.ElasticSearch8.Core;
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
            if (!properties.TryGetValue(fieldName, out var providerField))
            {
                providerField = CreateProperty(documentType, field);
                if (providerField != null)
                {
                    ConfigureProperty(documentType, providerField, field);
                    properties.Add(fieldName, providerField);
                }
            }

            var fieldValue = CombineValues(field, document.GetValueOrDefault(fieldName), GetFieldValue(documentType, fieldName, field, providerField));
            if (fieldValue != null)
            {
                document[fieldName] = fieldValue;
            }

            if (field.IsSuggestable)
            {
                var suggestionFieldName = GetSuggestionFieldName(documentType, field);
                if (!properties.TryGetValue(suggestionFieldName, out var suggestionField))
                {
                    suggestionField = CreateSuggestionProperty(documentType, field);
                    if (suggestionField != null)
                    {
                        properties.Add(suggestionFieldName, suggestionField);
                    }
                }

                var suggestionValue = GetSuggestionFieldValue(documentType, suggestionFieldName, field, suggestionField, fieldValue);
                if (suggestionValue != null)
                {
                    document[suggestionFieldName] = suggestionValue;
                }
            }
        }

        return document;

        static object CombineValues(IndexDocumentField field, object existingValue, object newValue)
        {
            if (existingValue is null)
            {
                return newValue;
            }

            if (newValue is null)
            {
                return existingValue;
            }

            var values = new List<object>();
            AddValues(values, existingValue);
            AddValues(values, newValue);

            return field.IsCollection || values.Count > 1 ? values : values[0];
        }

        static void AddValues(List<object> allValues, object value)
        {
            if (value is IEnumerable<object> values)
            {
                allValues.AddRange(values);
            }
            else if (value != null)
            {
                allValues.Add(value);
            }
        }
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

    protected virtual string GetSuggestionFieldName(string documentType, IndexDocumentField field)
    {
        return field.Name.ToSuggestionFieldName();
    }

    protected virtual object GetSuggestionFieldValue(string documentType, string fieldName, IndexDocumentField field, IProperty property, object value)
    {
        var inputs = value is null ? [] : GetCompletionInputs(value);

        return inputs.Length > 0 ? new Dictionary<string, object> { { "input", inputs } } : null;
    }

    protected static string[] GetCompletionInputs(object value)
    {
        var inputs = new List<string>();

        if (value is IEnumerable<object> values)
        {
            inputs.AddRange(values.Where(x => x != null).SelectMany(item => GetTokens(Convert.ToString(item, CultureInfo.InvariantCulture))));
        }
        else if (value != null)
        {
            inputs.AddRange(GetTokens(Convert.ToString(value, CultureInfo.InvariantCulture)));
        }

        return inputs.Count != 0
            ? inputs.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
    }

    protected static IEnumerable<string> GetTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var tokens = text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
        {
            yield break;
        }

        var maxTokens = Math.Min(tokens.Length, ModuleConstants.SuggestionFieldTokens);
        for (var length = 1; length <= maxTokens; length++)
        {
            var phrase = string.Join(" ", tokens.Take(length));
            if (phrase.Length <= ModuleConstants.SuggestionFieldLength)
            {
                yield return phrase;
            }
        }
    }

    protected virtual IProperty CreateProperty(string documentType, IndexDocumentField field)
    {
        return propertyService.CreateProperty(field);
    }

    protected virtual IProperty CreateSuggestionProperty(string documentType, IndexDocumentField field)
    {
        return propertyService.CreateSuggestionProperty(field);
    }

    protected virtual void ConfigureProperty(string documentType, IProperty providerField, IndexDocumentField field)
    {
        propertyService.ConfigureProperty(providerField, field);
    }
}
