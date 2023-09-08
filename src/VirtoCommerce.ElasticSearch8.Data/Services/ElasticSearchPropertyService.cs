using System;
using System.Collections.Generic;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public class ElasticSearchPropertyService : IElasticSearchPropertyService
    {
        public virtual IProperty CreateProperty(IndexDocumentField field)
        {
            return field.ValueType switch
            {
                IndexDocumentFieldValueType.Undefined => CreateProviderFieldByValue(field),
                IndexDocumentFieldValueType.Complex => new NestedProperty(),
                IndexDocumentFieldValueType.String when field.IsFilterable => new KeywordProperty(),
                IndexDocumentFieldValueType.String when !field.IsFilterable => new TextProperty(),
                IndexDocumentFieldValueType.Char => new KeywordProperty(),
                IndexDocumentFieldValueType.Guid => new KeywordProperty(),
                IndexDocumentFieldValueType.Integer => new IntegerNumberProperty(),
                IndexDocumentFieldValueType.Short => new ShortNumberProperty(),
                IndexDocumentFieldValueType.Byte => new ByteNumberProperty(),
                IndexDocumentFieldValueType.Long => new LongNumberProperty(),
                IndexDocumentFieldValueType.Float => new FloatNumberProperty(),
                IndexDocumentFieldValueType.Decimal => new DoubleNumberProperty(),
                IndexDocumentFieldValueType.Double => new DoubleNumberProperty(),
                IndexDocumentFieldValueType.DateTime => new DateProperty(),
                IndexDocumentFieldValueType.Boolean => new BooleanProperty(),
                IndexDocumentFieldValueType.GeoPoint => new GeoPointProperty(),
                _ => throw new ArgumentException($"Field '{field.Name}' has unsupported type '{field.ValueType}'", nameof(field))
            };
        }

        public virtual void ConfigureProperty(IProperty property, IndexDocumentField field)
        {
            if (property == null)
            {
                return;
            }

            switch (property)
            {
                //todo: fix object serialization
                //case NestedProperty nestedProperty:
                //break;
                case IntegerNumberProperty integerNumberProperty:
                    integerNumberProperty.Store = field.IsRetrievable;
                    break;
                case ShortNumberProperty shortNumberProperty:
                    shortNumberProperty.Store = field.IsRetrievable;
                    break;
                case ByteNumberProperty byteNumberProperty:
                    byteNumberProperty.Store = field.IsRetrievable;
                    break;
                case LongNumberProperty longNumberProperty:
                    longNumberProperty.Store = field.IsRetrievable;
                    break;
                case FloatNumberProperty floatNumberProperty:
                    floatNumberProperty.Store = field.IsRetrievable;
                    break;
                case DoubleNumberProperty doubleNumberProperty:
                    doubleNumberProperty.Store = field.IsRetrievable;
                    break;
                case DateProperty dateProperty:
                    dateProperty.Store = field.IsRetrievable;
                    break;
                case BooleanProperty booleanProperty:
                    booleanProperty.Store = field.IsRetrievable;
                    break;
                case GeoPointProperty geoPointProperty:
                    geoPointProperty.Store = field.IsRetrievable;
                    break;
                case TextProperty textProperty:
                    ConfigureTextProperty(textProperty, field);
                    break;
                case KeywordProperty keywordProperty:
                    ConfigureKeywordProperty(keywordProperty, field);
                    break;
            }
        }

        protected virtual IProperty CreateProviderFieldByValue(IndexDocumentField field)
        {
            if (field.Value == null)
            {
                throw new ArgumentException($"Field '{field.Name}' has no value", nameof(field));
            }

            var fieldType = field.Value.GetType();

            if (IsComplexType(fieldType))
            {
                return new NestedProperty();
            }

            return fieldType.Name switch
            {
                "String" => field.IsFilterable ? new KeywordProperty() : new TextProperty(),
                "Int32" => new IntegerNumberProperty(),
                "UInt16" => new IntegerNumberProperty(),
                "Int16" => new ShortNumberProperty(),
                "Byte" => new ByteNumberProperty(),
                "SByte" => new ByteNumberProperty(),
                "Int64" => new LongNumberProperty(),
                "UInt32" => new LongNumberProperty(),
                "TimeSpan" => new LongNumberProperty(),
                "Single" => new FloatNumberProperty(),
                "Decimal" => new DoubleNumberProperty(),
                "Double" => new DoubleNumberProperty(),
                "UInt64" => new DoubleNumberProperty(),
                "DateTime" => new DateProperty(),
                "DateTimeOffset" => new DateProperty(),
                "Boolean" => new BooleanProperty(),
                "Char" => new KeywordProperty(),
                "Guid" => new KeywordProperty(),
                "GeoPoint" => new GeoPointProperty(),
                _ => throw new ArgumentException($"Field '{field.Name}' has unsupported type '{fieldType}'", nameof(field))
            };
        }

        private static bool IsComplexType(Type type)
        {
            return
                type.IsAssignableTo(typeof(IEntity)) ||
                type.IsAssignableTo(typeof(IEnumerable<IEntity>));
        }

        protected virtual KeywordProperty ConfigureKeywordProperty(KeywordProperty keywordProperty, IndexDocumentField field)
        {
            keywordProperty.Store = field.IsRetrievable;
            keywordProperty.Index = field.IsFilterable;
            keywordProperty.Normalizer = "lowercase";

            keywordProperty.Fields = new Properties
            {
                { "raw", new KeywordProperty() },
            };

            if (field.IsSuggestable)
            {
                keywordProperty.Fields.Add(new PropertyName(ModuleConstants.CompletionSubFieldName), new CompletionProperty()
                {
                    MaxInputLength = ModuleConstants.SuggestionFieldLength,
                });
            }

            return keywordProperty;
        }

        protected virtual TextProperty ConfigureTextProperty(TextProperty textProperty, IndexDocumentField field)
        {
            textProperty.Store = field.IsRetrievable;
            textProperty.Index = field.IsSearchable;
            textProperty.Analyzer = field.IsSearchable ? ModuleConstants.SearchableFieldAnalyzerName : null;

            if (field.IsSuggestable)
            {
                textProperty.Fields ??= new Properties();
                textProperty.Fields.Add(new PropertyName(ModuleConstants.CompletionSubFieldName), new CompletionProperty()
                {
                    MaxInputLength = ModuleConstants.SuggestionFieldLength,
                });
            }

            return textProperty;
        }
    }
}
