using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Ingest;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearch8.Data.Services
{
    public partial class ElasticSearch8Provider : ISearchProvider, ISupportIndexSwap, ISupportIndexCreate
    {
        private readonly SearchOptions _searchOptions;
        private readonly ISettingsManager _settingsManager;
        private readonly IElasticSearchRequestBuilder _searchRequestBuilder;
        private readonly IElasticSearchResponseBuilder _searchResponseBuilder;
        private readonly IElasticSearchPropertyService _propertyService;
        private readonly ILogger<ElasticSearch8Provider> _logger;

        private readonly ConcurrentDictionary<string, IDictionary<PropertyName, IProperty>> _mappings = new();

        protected ElasticsearchClient Client { get; }
        protected Uri ServerUrl { get; }

        [GeneratedRegex("[/+_=]", RegexOptions.Compiled)]
        private static partial Regex SpecialSymbols();

        // prefixes for index aliases
        public string ActiveIndexAlias => GetActiveIndexAlias();
        public string BackupIndexAlias => GetBackupIndexAlias();

        public ElasticSearch8Provider(
            IOptions<SearchOptions> searchOptions,
            IOptions<ElasticSearch8Options> elasticOptions,
            ISettingsManager settingsManager,
            IElasticSearchRequestBuilder searchRequestBuilder,
            IElasticSearchResponseBuilder searchResponseBuilder,
            IElasticSearchPropertyService propertyService,
            ILogger<ElasticSearch8Provider> logger
            )
        {
            _searchOptions = searchOptions.Value;
            _settingsManager = settingsManager;
            _searchRequestBuilder = searchRequestBuilder;
            _searchResponseBuilder = searchResponseBuilder;
            _propertyService = propertyService;
            _logger = logger;

            if (!string.IsNullOrEmpty(elasticOptions.Value.Server))
            {
                ServerUrl = new Uri(elasticOptions.Value.Server);
                var settings = new ElasticsearchClientSettings(ServerUrl);

                if (elasticOptions.Value.EnableDebugMode)
                {
                    settings = settings.EnableDebugMode().OnRequestCompleted(response =>
                    {
                        if (response.HasSuccessfulStatusCode)
                        {
                            logger.LogDebug("Elasticsearch request to {Uri} completed successfully with status code {HttpStatusCode}. Debug information: {DebugInformation}",
                                response.Uri,
                                response.HttpStatusCode,
                                response.DebugInformation);
                        }
                        else
                        {
                            logger.LogError("Elasticsearch request to {Uri} failed with status code {HttpStatusCode}. Debug information: {DebugInformation}",
                                response.Uri,
                                response.HttpStatusCode,
                                response.DebugInformation);
                        }
                    });
                }

                if (!string.IsNullOrWhiteSpace(elasticOptions.Value.CertificateFingerprint))
                {
                    settings = settings.CertificateFingerprint(elasticOptions.Value.CertificateFingerprint);
                }

                settings = settings.Authentication(new BasicAuthentication(elasticOptions.Value.User, elasticOptions.Value.Key));

                Client = new ElasticsearchClient(settings);
            }
        }

        public Task<SearchResponse> SearchAsync(string documentType, SearchModule.Core.Model.SearchRequest request)
        {
            CheckClientCreated();

            return InternalSearchAsync(documentType, request);
        }

        public Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            CheckClientCreated();

            return InternalIndexAsync(documentType, documents, new IndexingParameters());
        }

        public Task DeleteIndexAsync(string documentType)
        {
            CheckClientCreated();

            return InternalDeleteIndexAsync(documentType);
        }

        public Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            CheckClientCreated();

            return InternalRemoveAsync(documentType, documents);
        }

        public Task<IndexingResult> IndexWithBackupAsync(string documentType, IList<IndexDocument> documents)
        {
            CheckClientCreated();

            return InternalIndexAsync(documentType, documents, new IndexingParameters() { Reindex = true });
        }

        public Task SwapIndexAsync(string documentType)
        {
            CheckClientCreated();

            return InternalSwapIndexAsync(documentType);
        }

        /// <summary>
        /// Puts an active alias on a default index (if exists)
        /// </summary>
        public Task AddActiveAlias(IEnumerable<string> documentTypes)
        {
            CheckClientCreated();

            return InternalAddActiveAlias(documentTypes);
        }

        public Task CreateIndexAsync(string documentType, IndexDocument schema)
        {
            CheckClientCreated();

            return InternalCreateIndexAsync(documentType, [schema], new IndexingParameters { Reindex = true });
        }

        protected virtual async Task<SearchResponse> InternalSearchAsync(string documentType, SearchModule.Core.Model.SearchRequest request)
        {
            var indexName = GetIndexName(request.UseBackupIndex, documentType);
            SearchResponse<SearchDocument> providerResponse;

            try
            {
                var availableFields = await GetMappingAsync(indexName);
                var providerRequest = _searchRequestBuilder.BuildRequest(request, indexName, documentType, availableFields);
                providerResponse = await Client.SearchAsync<SearchDocument>(providerRequest);
            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            if (!providerResponse.IsValidResponse && providerResponse.ApiCallDetails.HttpStatusCode != (int)HttpStatusCode.NotFound)
            {
                ThrowException(providerResponse.DebugInformation, providerResponse.ApiCallDetails.OriginalException);
            }

            var result = _searchResponseBuilder.ToSearchResponse(providerResponse, request);
            return result;
        }

        protected virtual async Task InternalDeleteIndexAsync(string documentType)
        {
            try
            {
                //get backup index by alias and delete if present
                var indexAlias = GetIndexAlias(BackupIndexAlias, documentType);
                var indexName = await GetIndexNameAsync(indexAlias);

                if (indexName != null)
                {
                    var response = await Client.Indices.DeleteAsync(indexName);
                    if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode != (int)HttpStatusCode.NotFound)
                    {
                        throw new SearchException(response.DebugInformation);
                    }
                }

                RemoveMappingFromCache(indexAlias);
            }
            catch (Exception ex)
            {
                ThrowException("Failed to delete index", ex);
            }
        }

        protected virtual async Task<IndexingResult> InternalRemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            var indexName = GetIndexAlias(ActiveIndexAlias, documentType);

            var providerDocuments = documents.Select(d => new SearchDocument { Id = d.Id }).ToArray();

            var bulkResponse = await Client.BulkAsync(x => CreateBulkDeleteRequest(indexName, providerDocuments, x));

            await Client.Indices.RefreshAsync(indexName);

            var result = new IndexingResult
            {
                Items = bulkResponse.Items.Select(i => new IndexingResultItem
                {
                    Id = i.Id,
                    Succeeded = i.IsValid,
                    ErrorMessage = i.Error?.Reason
                }).ToArray()
            };

            return result;
        }

        protected virtual async Task InternalSwapIndexAsync(string documentType)
        {
            ArgumentNullException.ThrowIfNull(documentType);

            // get active index and alias
            var activeIndexAlias = GetIndexAlias(ActiveIndexAlias, documentType);

            // if no active index found - check that default (active) index, if not create, if does assign the alias to it
            var indexExists = await IndexExistsAsync(activeIndexAlias);
            if (!indexExists)
            {
                var indexName = GetIndexName(documentType);
                var indexExits = await IndexExistsAsync(indexName);
                if (!indexExits)
                {
                    // create new index with alias
                    await CreateIndexAsync(indexName, activeIndexAlias);
                }
                else
                {
                    // attach alias to default index
                    await Client.Indices.PutAliasAsync(indexName, activeIndexAlias);
                }

            }

            // swap start
            var activeIndexName = await GetIndexNameAsync(activeIndexAlias);
            if (activeIndexName == null)
            {
                return;
            }

            var bulkAliasDescriptorActions = new List<IndexUpdateAliasesAction>
            {
                new RemoveAction { Index = activeIndexName, Alias = activeIndexAlias }
            };

            var backupIndexAlias = GetIndexAlias(BackupIndexAlias, documentType);
            var backupIndexName = await GetIndexNameAsync(backupIndexAlias);

            if (backupIndexName != null)
            {
                bulkAliasDescriptorActions.Add(new RemoveAction { Index = backupIndexName, Alias = backupIndexAlias });
                bulkAliasDescriptorActions.Add(new AddAction { Index = backupIndexName, Alias = activeIndexAlias });
            }

            bulkAliasDescriptorActions.Add(new AddAction { Index = activeIndexName, Alias = backupIndexAlias });

            var bulkAliasDescriptor = new UpdateAliasesRequestDescriptor();
            bulkAliasDescriptor.Actions(bulkAliasDescriptorActions);
            var swapResponse = await Client.Indices.UpdateAliasesAsync(bulkAliasDescriptor);

            if (!swapResponse.IsValidResponse)
            {
                ThrowException($"Failed to swap indexes for the document type {documentType}. {swapResponse.DebugInformation}", swapResponse.ApiCallDetails.OriginalException);
            }

            RemoveMappingFromCache(backupIndexAlias);
            RemoveMappingFromCache(activeIndexAlias);
        }

        protected async Task InternalAddActiveAlias(IEnumerable<string> documentTypes)
        {
            try
            {
                foreach (var documentType in documentTypes)
                {
                    var indexAlias = GetIndexAlias(ActiveIndexAlias, documentType);
                    if (await IndexExistsAsync(indexAlias))
                    {
                        continue;
                    }

                    var indexName = GetIndexName(documentType);
                    if (!await IndexExistsAsync(indexName))
                    {
                        continue;
                    }

                    var aliasResponse = await Client.Indices.PutAliasAsync(indexName, indexAlias);
                    if (!aliasResponse.IsValidResponse)
                    {
                        throw new SearchException(aliasResponse.DebugInformation);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while putting an active alias on a default index at {nameof(AddActiveAlias)}. Possible fail on Elastic server side at IndexExists check.");
            }
        }

        protected virtual async Task<IndexingResult> InternalIndexAsync(string documentType, IList<IndexDocument> documents, IndexingParameters parameters)
        {
            var createIndexResult = await InternalCreateIndexAsync(documentType, documents, parameters);

            var pipelines = new List<string>();

            if (_settingsManager.GetSemanticSearchEnabled())
            {
                // Check if ML field is created
                await CreateMLField(createIndexResult.IndexName);

                var pipelineName = _settingsManager.GetPipelineName();

                // Check if ML pipeline created
                await CheckMLPipeline(pipelineName);

                pipelines.Add(pipelineName);
            }

            var bulkResponse = await Client.BulkAsync(x => CreateBulkIndexRequest(createIndexResult.IndexName, createIndexResult.ProviderDocuments, x, pipelines));

            await Client.Indices.RefreshAsync(Indices.Index(createIndexResult.IndexName));

            var result = new IndexingResult
            {
                Items = new List<IndexingResultItem>()
            };

            if (!bulkResponse.IsValidResponse)
            {
                result.Items.Add(new IndexingResultItem
                {
                    Id = ModuleConstants.ElasticSearchExceptionTitle,
                    ErrorMessage = bulkResponse.ApiCallDetails?.OriginalException?.Message,
                    Succeeded = false
                });
            }

            if (bulkResponse.Items != null)
            {
                result.Items.AddRange(bulkResponse.Items.Select(i => new IndexingResultItem
                {
                    Id = i.Id,
                    Succeeded = i.IsValid,
                    ErrorMessage = i.Error?.Reason
                }));
            }

            return result;
        }

        private async Task CheckMLPipeline(string pipelineName)
        {
            var getPipelineRequest = new GetPipelineRequest(pipelineName)
            {
                Summary = true
            };

            var pipelineResult = await Client.Ingest.GetPipelineAsync(getPipelineRequest);

            if (pipelineResult.ApiCallDetails.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new SearchException($"ML pipeline is not found: {pipelineName}. Please create the pipeline first.");
            }
        }

        private async Task CreateMLField(string indexName)
        {
            var indexMappings = await GetMappingAsync(indexName);

            if (!indexMappings.ContainsKey(ModuleConstants.ModelPropertyName))
            {
                var properties = default(Properties);

                var semanticSearchModelType = _settingsManager.GetSemanticSearchType();
                switch (semanticSearchModelType)
                {
                    case ModuleConstants.ElserModel:
                        properties = new Properties
                        {
                            { ModuleConstants.TokensPropertyName, new SparseVectorProperty() }
                        };
                        break;
                    case ModuleConstants.ThirdPartyModel:
                        properties = new Properties
                        {
                            { ModuleConstants.VectorPropertyName, new DenseVectorProperty
                                            {
                                                Index = true,
                                                Dims = _settingsManager.GetVectorModelDimensionsCount(),
                                                Similarity = DenseVectorSimilarity.Cosine,
                                            }
                            }
                        };
                        break;
                }

                if (properties is null)
                {
                    return;
                }

                var request = new PutMappingRequest(indexName) { Properties = properties };
                var response = await Client.Indices.PutMappingAsync(request);

                if (!response.ApiCallDetails.HasSuccessfulStatusCode)
                {
                    throw new SearchException($"Failed to create {ModuleConstants.ModelPropertyName} field", response.ApiCallDetails.OriginalException);
                }
            }
        }

        private static void CreateBulkIndexRequest(string indexName, IList<SearchDocument> documents, BulkRequestDescriptor descriptor, List<string> pipelines)
        {
            descriptor
                .Index((IndexName)indexName)
                .IndexMany(documents);

            foreach (var pipeline in pipelines)
            {
                descriptor.Pipeline(pipeline);
            }
        }

        protected virtual async Task<CreateIndexResult> InternalCreateIndexAsync(string documentType, IList<IndexDocument> documents, IndexingParameters parameters)
        {
            var indexName = GetIndexName(parameters.Reindex, documentType);

            var mapping = await GetMappingAsync(indexName);
            var providerFields = new Properties(mapping);
            var oldFieldsCount = providerFields.Count();

            var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, providerFields)).ToList();

            var updateMapping = providerFields.Count() != oldFieldsCount;

            var indexExists = await IndexExistsAsync(indexName);

            if (!indexExists)
            {
                var newIndexName = GetIndexName(documentType, GetRandomIndexSuffix());
                await CreateIndexAsync(newIndexName, alias: indexName);
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

            if (mapping.IsNullOrEmpty())
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
                    if (!properties.TryGetValue(fieldName, out var providerField))
                    {
                        providerField = _propertyService.CreateProperty(field);
                        _propertyService.ConfigureProperty(providerField, field);
                        properties.Add(fieldName, providerField);
                    }

                    if (field.Name != "__object")
                    {
                        var value = GetFieldValue(providerField, field);
                        result.Add(fieldName, value);
                    }
                }
            }

            return result;
        }

        private static object GetFieldValue(IProperty property, IndexDocumentField field)
        {
            var isCollection = field.IsCollection || field.Values.Count > 1;
            object result;

            if (property is GeoPointProperty)
            {
                result = isCollection
                    ? field.Values.OfType<GeoPoint>().Select(x => x.ToElasticValue()).ToArray()
                    : (field.Value as GeoPoint)?.ToElasticValue();
            }
            else
            {
                result = isCollection
                    ? field.Values
                    : field.Value;
            }

            return result;
        }

        #region CreateIndex (move to index create service)

        protected virtual async Task CreateIndexAsync(string indexName, string alias)
        {
            var response = await Client.Indices.CreateAsync(indexName, i => i
                .Settings(x => ConfigureIndexSettings(x))
                .Aliases(x => x.Add(alias, new AliasDescriptor())
            ));

            if (!response.ApiCallDetails.HasSuccessfulStatusCode)
            {
                ThrowException("Failed to create index. " + response.DebugInformation, response.ApiCallDetails.OriginalException);
            }
        }

        protected virtual IndexSettingsDescriptor ConfigureIndexSettings(IndexSettingsDescriptor settings)
        {
            var fieldsLimit = _settingsManager.GetFieldsLimit();
            var ngramDiff = _settingsManager.GetMaxGram() - _settingsManager.GetMinGram();

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
                .NGram(ModuleConstants.NGramFilterName, ConfigureNGramFilter)
                .EdgeNGram(ModuleConstants.EdgeNGramFilterName, ConfigureEdgeNGramFilter);
        }

        protected virtual void ConfigureAnalyzers(AnalyzersDescriptor descriptor)
        {
            descriptor.Custom(ModuleConstants.SearchableFieldAnalyzerName, ConfigureSearchableFieldAnalyzer);
        }

        protected virtual void ConfigureNormalizers(NormalizersDescriptor descriptor)
        {
            descriptor.Custom("lowercase", ConfigureLowerCaseNormalizer);
        }

        private void ConfigureNGramFilter(NGramTokenFilterDescriptor descriptor)
        {
            descriptor.MinGram(_settingsManager.GetMinGram()).MaxGram(_settingsManager.GetMaxGram());
        }

        private void ConfigureEdgeNGramFilter(EdgeNGramTokenFilterDescriptor descriptor)
        {
            descriptor.MinGram(_settingsManager.GetMinGram()).MaxGram(_settingsManager.GetMaxGram());
        }

        protected virtual void ConfigureSearchableFieldAnalyzer(CustomAnalyzerDescriptor descriptor)
        {
            descriptor
                .Tokenizer("standard")
                .Filter(new List<string> { "lowercase", _settingsManager.GetTokenFilterName() });
        }

        protected virtual void ConfigureLowerCaseNormalizer(CustomNormalizerDescriptor descriptor)
        {
            descriptor.Filter(new List<string> { "lowercase" });
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
                          mappingResponse.Mappings.Values.FirstOrDefault()?.Mappings;

            return mapping?.Properties;
        }

        protected virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            var response = await Client.Indices.ExistsAsync(indexName);

            if (!response.IsSuccess())
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

        protected virtual string GetActiveIndexAlias()
        {
            return "active";
        }

        protected virtual string GetBackupIndexAlias()
        {
            return "backup";
        }

        private static void CreateBulkDeleteRequest(string indexName, IList<SearchDocument> documents, BulkRequestDescriptor descriptor)
        {
            var ids = documents.Select(x => new Id(x.Id));

            descriptor.DeleteMany(indexName, ids);
        }

        private async Task<IndexName> GetIndexNameAsync(string indexAlias)
        {
            var activeIndexResponse = await Client.Indices.GetAsync(new GetIndexRequest(indexAlias));
            if (!activeIndexResponse.IsValidResponse && activeIndexResponse.ApiCallDetails.HttpStatusCode != (int)HttpStatusCode.NotFound)
            {
                throw new SearchException(activeIndexResponse.DebugInformation);
            }

            return activeIndexResponse.Indices?.Keys?.FirstOrDefault();
        }

        protected virtual string GetIndexName(string documentType)
        {
            return string.Join("-", _searchOptions.GetScope(documentType), documentType).ToLowerInvariant();
        }

        protected virtual string GetIndexName(string documentType, string suffix)
        {
            return string.Join("-", _searchOptions.GetScope(documentType), documentType, suffix).ToLowerInvariant();
        }

        protected virtual string GetIndexName(bool useBackupIndex, string documentType)
        {
            var alias = useBackupIndex
                ? BackupIndexAlias
                : ActiveIndexAlias;

            return GetIndexAlias(alias, documentType);
        }

        /// <summary>
        /// Combine default index name and alias
        /// </summary>
        protected virtual string GetIndexAlias(string alias, string documentType)
        {
            return string.Join("-", GetIndexName(documentType), alias).ToLowerInvariant();
        }

        /// <summary>
        /// Gets random name suffix to attach to index (for automatic creation of backup indices)
        /// </summary>
        protected static string GetRandomIndexSuffix()
        {
            var result = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..10];
            result = SpecialSymbols().Replace(result, string.Empty);

            return result;
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out _);
        }

        private void CheckClientCreated()
        {
            if (Client == null)
            {
                throw new SearchException("Elastic Search 8 Provider connection is not configured. Please configure it in Search:ElasticSearch8 app configuration section.");
            }
        }


        protected class CreateIndexResult
        {
            public string IndexName { get; set; }

            public IList<SearchDocument> ProviderDocuments { get; set; }
        }

        protected class IndexingParameters
        {
            public bool Reindex { get; set; }
        }
    }
}
