using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using Microsoft.Extensions.ObjectPool;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Data.Services;

public class ElasticSearchDocumentConverter(IElasticSearchPropertyService propertyService) : IElasticSearchDocumentConverter
{
    private static readonly ObjectPool<StringBuilder> _stringBuilderPool = new DefaultObjectPoolProvider().CreateStringBuilderPool();

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
        var input = GetCompletionInputs(value);

        return input.Length > 0 ? new Dictionary<string, object> { { nameof(input), input } } : null;
    }

    protected static string[] GetCompletionInputs(object value, int maxLength = ModuleConstants.SuggestionFieldLength, int maxTokens = ModuleConstants.SuggestionFieldTokens)
    {
        if (value is null)
        {
            return [];
        }

        // SortedSet handles both deduplication and sorting in a single pass
        var inputs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (value is IEnumerable<object> values)
        {
            // Direct iteration avoids LINQ overhead
            foreach (var item in values)
            {
                if (item is null)
                {
                    continue;
                }

                var text = Convert.ToString(item, CultureInfo.InvariantCulture)?.ToLowerInvariant();
                foreach (var token in GetTokens(text, maxLength, maxTokens))
                {
                    inputs.Add(token);
                }
            }
        }
        else
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.ToLowerInvariant();
            foreach (var token in GetTokens(text, maxLength, maxTokens))
            {
                inputs.Add(token);
            }
        }

        return inputs.Count > 0 ? [.. inputs] : [];
    }

    protected static IEnumerable<string> GetTokens(string text, int maxLength, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield break;
        }

        var tokens = SplitText(text, maxTokens);
        if (tokens.Count == 0)
        {
            yield break;
        }

        var sb = _stringBuilderPool.Get();
        try
        {
            for (var token = 0; token < tokens.Count; token++)
            {
                if (token > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(tokens[token]);

                if (sb.Length <= maxLength)
                {
                    yield return sb.ToString();
                }
                else
                {
                    yield break;
                }
            }
        }
        finally
        {
            _stringBuilderPool.Return(sb);
        }
    }

    protected static List<string> SplitText(string text, int maxTokens)
    {
        var span = text.AsSpan();
        var tokens = new List<string>(maxTokens);
        var tokenStart = 0;

        // Manual tokenization using Span to avoid Split allocation
        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || IsTokenSeparator(span[i]))
            {
                if (i > tokenStart)
                {
                    var dirtyToken = span.Slice(tokenStart, i - tokenStart);
                    var token = TrimPunctuation(dirtyToken);

                    if (!token.IsEmpty && !token.IsWhiteSpace())
                    {
                        tokens.Add(new string(token));
                        if (tokens.Count >= maxTokens)
                        {
                            break;
                        }
                    }
                }
                tokenStart = i + 1;
            }
        }

        return tokens;
    }

    protected static bool IsTokenSeparator(char c)
    {
        return c is ' ' or '\t' or '\n' or '\r';
    }

    protected static bool IsPreservedSymbol(char c)
    {
        // Preserve only meaningful symbols in e-commerce and tech contexts: # % @ &
        return c is '#' or '%' or '@' or '&';
    }

    protected static bool ShouldTrimPunctuation(char c)
    {
        // Don't trim preserved symbols, trim punctuation characters
        return !IsPreservedSymbol(c) && char.IsPunctuation(c);
    }

    protected static ReadOnlySpan<char> TrimPunctuation(ReadOnlySpan<char> token)
    {
        if (token.IsEmpty)
        {
            return token;
        }

        var start = 0;
        while (start < token.Length && ShouldTrimPunctuation(token[start]))
        {
            start++;
        }

        var end = token.Length - 1;
        while (end >= start && ShouldTrimPunctuation(token[end]))
        {
            end--;
        }

        return start > end ? ReadOnlySpan<char>.Empty : token.Slice(start, end - start + 1);
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
