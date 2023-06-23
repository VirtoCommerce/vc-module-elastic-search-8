using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearch8x.Data.Extensions;
using VirtoCommerce.ElasticSearch8x.Data.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearch8x.Data.Services
{
    public class ElasticSearch8xProvider : ISearchProvider
    {
        private readonly SearchOptions _searchOptions;
        private readonly ElasticSearch8xOptions _elasticOptions;
        private readonly ISettingsManager _settingsManager;
        private readonly SearchRequestBuilder _searchRequestBuilder;
        private readonly SearchResponseBuilder _searchResponseBuilder;

        protected const string ExceptionTitle = "Elasticsearch8 Server";
        protected const string ActiveIndexAlias = "active";
        protected const string BackupIndexAlias = "backup";
        public string SearchableFieldAnalyzerName { get; private set; } = "searchable_field_analyzer";
        public string NGramFilterName { get; private set; } = "custom_ngram";
        public string EdgeNGramFilterName { get; private set; } = "custom_edge_ngram";
        protected const string CompletionSubFieldName = "completion";
        protected const int SuggestionFieldLength = 256;

        private readonly ConcurrentDictionary<string, IDictionary<PropertyName, IProperty>> _mappings = new();

        protected ElasticsearchClient Client { get; }
        protected Uri ServerUrl { get; }

        public ElasticSearch8xProvider(
            IOptions<SearchOptions> searchOptions,
            IOptions<ElasticSearch8xOptions> elasticOptions,
            ISettingsManager settingsManager,
            SearchRequestBuilder searchRequestBuilder,
            SearchResponseBuilder searchResponseBuilder
            )
        {
            _searchOptions = searchOptions.Value;
            _elasticOptions = elasticOptions.Value;
            _settingsManager = settingsManager;
            _searchRequestBuilder = searchRequestBuilder;
            _searchResponseBuilder = searchResponseBuilder;

            ServerUrl = new Uri(_elasticOptions.Server);
            var settings = new ElasticsearchClientSettings(ServerUrl);

            if (!string.IsNullOrWhiteSpace(_elasticOptions.CertificateFingerprint))
            {
                settings = settings.CertificateFingerprint(_elasticOptions.CertificateFingerprint);
            }

            settings = settings.Authentication(new BasicAuthentication(_elasticOptions.User, _elasticOptions.Key));

            Client = new ElasticsearchClient(settings);
        }

        public async Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            return await InternalIndexAsync(documentType, documents);
        }

        protected virtual async Task<IndexingResult> InternalIndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = new IndexingResult
            {
                Items = new List<IndexingResultItem>()
            };

            var createIndexResult = await InternalCreateIndexAsync(documentType, documents);

            var bulkResponse = await Client.BulkAsync(x => CreateBulkRequest(createIndexResult.IndexName, createIndexResult.ProviderDocuments, x));

            await Client.Indices.RefreshAsync(Indices.Index(createIndexResult.IndexName));

            if (!bulkResponse.IsValidResponse)
            {
                result.Items.Add(new IndexingResultItem
                {
                    Id = ExceptionTitle,
                    ErrorMessage = bulkResponse.ApiCallDetails?.OriginalException?.Message,
                    Succeeded = false
                });
            }

            result.Items.AddRange(bulkResponse.Items.Select(i => new IndexingResultItem
            {
                Id = i.Id,
                Succeeded = i.IsValid,
                ErrorMessage = i.Error?.Reason
            }));

            return result;
        }

        private BulkRequestDescriptor CreateBulkRequest(string indexName, IList<SearchDocument> documents, BulkRequestDescriptor obj)
        {
            obj = obj
                .Index(indexName)
                .IndexMany(documents);

            return obj;
        }

        protected virtual async Task<CreateIndexResult> InternalCreateIndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var indexName = GetIndexName(documentType);

            var mapping = await GetMappingAsync(indexName);
            var providerFields = new Properties(mapping);
            var oldFieldsCount = providerFields.Count();

            var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, providerFields)).ToList();

            var updateMapping = providerFields.Count() != oldFieldsCount;

            var indexExists = await IndexExistsAsync(indexName);

            if (!indexExists)
            {
                await CreateIndexAsync(indexName, alias: indexName);
            }

            if (!indexExists || updateMapping)
            {
                await UpdateMappingAsync(indexName, providerFields);
            }

            return new CreateIndexResult
            {
                IndexName = indexName,
                ProviderDocuments = providerDocuments,
            };
        }

        protected virtual async Task UpdateMappingAsync(string indexName, Properties properties)
        {
            Properties newProperties;
            Properties allProperties;

            var mapping = await LoadMappingAsync(indexName);
            var existingProperties = new Properties(mapping);

            if (existingProperties == null)
            {
                newProperties = properties;
                allProperties = properties;
            }
            else
            {
                newProperties = new Properties();
                allProperties = existingProperties;

                foreach (var (name, value) in properties)
                {
                    if (!existingProperties.TryGetProperty(name.Name, out _))
                    {
                        newProperties.Add(name, value);
                        allProperties.Add(name, value);
                    }
                }
            }

            if (newProperties.Any())
            {
                var request = new PutMappingRequest(indexName) { Properties = newProperties };
                var response = await Client.Indices.PutMappingAsync(request);

                if (!response.IsValidResponse)
                {
                    ThrowException("Failed to submit mapping. " + response.DebugInformation, response.ApiCallDetails.OriginalException);
                }
            }

            AddMappingToCache(indexName, allProperties);
            await Client.Indices.RefreshAsync(indexName);
        }


        protected virtual SearchDocument ConvertToProviderDocument(IndexDocument document, IDictionary<PropertyName, IProperty> properties)
        {
            var result = new SearchDocument { Id = document.Id };

            foreach (var field in document.Fields.OrderBy(f => f.Name))
            {
                var fieldName = field.Name.ToElasticFieldName();

                if (result.ContainsKey(fieldName))
                {
                    var newValues = new List<object>();
                    var currentValue = result[fieldName];

                    if (currentValue is object[] currentValues)
                    {
                        newValues.AddRange(currentValues);
                    }
                    else
                    {
                        newValues.Add(currentValue);
                    }

                    newValues.AddRange(field.Values);
                    result[fieldName] = newValues.ToArray();
                }
                else
                {
                    if (!properties.ContainsKey(fieldName))
                    {
                        var providerField = CreateProviderFieldByType(field);
                        ConfigureProperty(providerField, field);
                        properties.Add(fieldName, providerField);
                    }

                    var isCollection = field.IsCollection || field.Values.Count > 1;
                    object value;

                    if (field.Value is GeoPoint point)
                    {
                        value = isCollection
                            ? field.Values.Select(v => ((GeoPoint)v).ToElasticValue()).ToArray()
                            : point.ToElasticValue();
                    }
                    else
                    {
                        value = isCollection
                            ? field.Values
                            : field.Value;
                    }

                    result.Add(fieldName, value);
                }
            }

            return result;
        }

        protected virtual IProperty CreateProviderFieldByType(IndexDocumentField field)
        {
            return field.ValueType switch
            {
                IndexDocumentFieldValueType.Undefined => CreateProviderField(field),
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

        protected virtual IProperty CreateProviderField(IndexDocumentField field)
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

        protected virtual void ConfigureProperty(IProperty property, IndexDocumentField field)
        {
            if (property == null)
            {
                return;
            }

            switch (property)
            {
                case NestedProperty nestedProperty:
                    var objects = field.Value.GetPropertyNames<object>(deep: 7);
                    nestedProperty.Properties = new Properties(objects.ToDictionary(x => new PropertyName(x), _ => (IProperty)new TextProperty()));
                    break;
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
                keywordProperty.Fields.Add(new PropertyName(CompletionSubFieldName), new CompletionProperty()
                {
                    //Name = field.Name,
                    MaxInputLength = SuggestionFieldLength,
                });
            }

            return keywordProperty;
        }

        protected virtual TextProperty ConfigureTextProperty(TextProperty textProperty, IndexDocumentField field)
        {
            textProperty.Store = field.IsRetrievable;
            textProperty.Index = field.IsSearchable;
            textProperty.Analyzer = field.IsSearchable ? SearchableFieldAnalyzerName : null;

            if (field.IsSuggestable)
            {
                textProperty.Fields ??= new Properties();
                textProperty.Fields.Add(new PropertyName(CompletionSubFieldName), new CompletionProperty()
                {
                    //Name = field.Name,
                    MaxInputLength = SuggestionFieldLength,
                });
            }

            return textProperty;
        }

        #region CreateIndex (move to index create service)

        protected virtual async Task CreateIndexAsync(string indexName, string alias)
        {
            var response = await Client.Indices.CreateAsync(indexName, i => i
                .Settings(x => ConfigureIndexSettings(x))
            //.Aliases(x => ConfigureAliases(x, alias))
            );

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            {
                ThrowException("Failed to create index. " + response.DebugInformation, response.ApiCallDetails.OriginalException);
            }
        }

        protected virtual IndexSettingsDescriptor ConfigureIndexSettings(IndexSettingsDescriptor settings)
        {
            var fieldsLimit = GetFieldsLimit();
            var ngramDiff = GetMaxGram() - GetMinGram();

            return settings
                .MaxNgramDiff(ngramDiff)
                .Mapping(mappingDescriptor => ConfigureMappingLimit(mappingDescriptor, fieldsLimit))
                .Analysis(analysisDescriptor => analysisDescriptor
                    .TokenFilters(ConfigureTokenFilters)
                    .Analyzers(ConfigureAnalyzers)
                    .Normalizers(ConfigureNormalizers)
                );
        }

        protected virtual void ConfigureMappingLimit(MappingLimitSettingsDescriptor descriptor, int fieldsLimit)
        {
            descriptor.TotalFields(new MappingLimitSettingsTotalFields { Limit = fieldsLimit });
        }

        protected virtual void ConfigureTokenFilters(TokenFiltersDescriptor descriptor)
        {
            descriptor
                .NGram(NGramFilterName, ConfigureNGramFilter)
                .EdgeNGram(EdgeNGramFilterName, ConfigureEdgeNGramFilter);
        }

        protected virtual void ConfigureAnalyzers(AnalyzersDescriptor descriptor)
        {
            descriptor.Custom(SearchableFieldAnalyzerName, ConfigureSearchableFieldAnalyzer);
        }

        protected virtual void ConfigureNormalizers(NormalizersDescriptor descriptor)
        {
            descriptor.Custom("lowercase", ConfigureLowerCaseNormaliser);
        }

        private void ConfigureNGramFilter(NGramTokenFilterDescriptor descriptor)
        {
            descriptor.MinGram(GetMinGram()).MaxGram(GetMaxGram());
        }

        private void ConfigureEdgeNGramFilter(EdgeNGramTokenFilterDescriptor descriptor)
        {
            descriptor.MinGram(GetMinGram()).MaxGram(GetMaxGram());
        }

        protected virtual void ConfigureSearchableFieldAnalyzer(CustomAnalyzerDescriptor descriptor)
        {
            descriptor
                .Tokenizer("standard")
                .Filter(new List<string> { "lowercase", GetTokenFilterName() });
        }

        protected virtual void ConfigureLowerCaseNormaliser(CustomNormalizerDescriptor descriptor)
        {
            descriptor.Filter(new List<string> { "lowercase" });
        }

        protected virtual int GetFieldsLimit()
        {
            return 1000;
        }

        protected virtual string GetTokenFilterName()
        {
            return "custom_edge_ngram";
        }

        protected virtual int GetMinGram()
        {
            return 1;
        }

        protected virtual int GetMaxGram()
        {
            return 20;
        }

        #endregion

        protected virtual async Task<IDictionary<PropertyName, IProperty>> GetMappingAsync(string indexName)
        {
            if (GetMappingFromCache(indexName, out var properties))
            {
                return properties;
            }

            if (await IndexExistsAsync(indexName))
            {
                properties = await LoadMappingAsync(indexName);
            }

            properties ??= new Properties<IDictionary<PropertyName, IProperty>>();
            AddMappingToCache(indexName, properties);

            return properties;
        }

        protected virtual bool GetMappingFromCache(string indexName, out IDictionary<PropertyName, IProperty> properties)
        {
            return _mappings.TryGetValue(indexName, out properties);
        }

        protected virtual async Task<IDictionary<PropertyName, IProperty>> LoadMappingAsync(string indexName)
        {
            var mappingResponse = await Client.Indices.GetMappingAsync(new GetMappingRequest(indexName));

            var mapping = mappingResponse.GetMappingFor(indexName) ??
                          mappingResponse.Indices.Values.FirstOrDefault()?.Mappings;

            return mapping?.Properties;
        }

        protected virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            var response = await Client.Indices.ExistsAsync(indexName);

            if (response.IsSuccess() == false)
            {
                ThrowException($"Index check call failed for index: {indexName}", response.ApiCallDetails.OriginalException);
            }

            return response.Exists;
        }

        protected virtual void AddMappingToCache(string indexName, IDictionary<PropertyName, IProperty> properties)
        {
            _mappings[indexName] = properties;
        }

        protected virtual void ThrowException(string message, Exception innerException)
        {
            throw new SearchException($"{message}. URL:{ServerUrl}, Scope: {_searchOptions.Scope}", innerException);
        }

        public async Task<SearchResponse> SearchAsync(string documentType, SearchModule.Core.Model.SearchRequest request)
        {
            var indexName = GetIndexName(documentType);
            SearchResponse<SearchDocument> providerResponse;

            try
            {
                var availableFields = await GetMappingAsync(indexName);
                var providerRequest = _searchRequestBuilder.BuildRequest(request, indexName, availableFields);
                providerResponse = await Client.SearchAsync<SearchDocument>(providerRequest);

            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            if (!providerResponse.IsValidResponse)
            {
                ThrowException(providerResponse.DebugInformation, providerResponse.ApiCallDetails.OriginalException);
            }

            var result = _searchResponseBuilder.ToSearchResponse(providerResponse, request);
            return result;
        }

        /// <summary>
        /// TODO
        /// </summary>
        public Task DeleteIndexAsync(string documentType)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// TODO
        /// </summary>
        public Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            throw new NotImplementedException();
        }

        protected virtual string GetIndexName(string documentType)
        {
            return string.Join("-", _searchOptions.GetScope(documentType), documentType).ToLowerInvariant();
        }
    }

    public class CreateIndexResult
    {
        public string IndexName { get; set; }

        public IList<SearchDocument> ProviderDocuments { get; set; }
    }
}