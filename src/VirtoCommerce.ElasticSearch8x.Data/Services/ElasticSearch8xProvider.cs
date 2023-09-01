using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Analysis;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Clients.Elasticsearch.Ingest;
using Elastic.Clients.Elasticsearch.Mapping;
using Elastic.Transport;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearch8x.Core;
using VirtoCommerce.ElasticSearch8x.Core.Models;
using VirtoCommerce.ElasticSearch8x.Core.Services;
using VirtoCommerce.ElasticSearch8x.Data.Extensions;
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
        private readonly ISettingsManager _settingsManager;
        private readonly IElasticSearchRequestBuilder _searchRequestBuilder;
        private readonly IElasticSearchResponseBuilder _searchResponseBuilder;
        private readonly IElasticSearchPropertyService _propertyService;

        private readonly ConcurrentDictionary<string, IDictionary<PropertyName, IProperty>> _mappings = new();

        protected ElasticsearchClient Client { get; }
        protected Uri ServerUrl { get; }

        public ElasticSearch8xProvider(
            IOptions<SearchOptions> searchOptions,
            IOptions<ElasticSearch8xOptions> elasticOptions,
            ISettingsManager settingsManager,
            IElasticSearchRequestBuilder searchRequestBuilder,
            IElasticSearchResponseBuilder searchResponseBuilder,
            IElasticSearchPropertyService propertyService
            )
        {
            _searchOptions = searchOptions.Value;
            _settingsManager = settingsManager;
            _searchRequestBuilder = searchRequestBuilder;
            _searchResponseBuilder = searchResponseBuilder;
            _propertyService = propertyService;

            ServerUrl = new Uri(elasticOptions.Value.Server);
            var settings = new ElasticsearchClientSettings(ServerUrl);

            if (!string.IsNullOrWhiteSpace(elasticOptions.Value.CertificateFingerprint))
            {
                settings = settings.CertificateFingerprint(elasticOptions.Value.CertificateFingerprint);
            }

            settings = settings.Authentication(new BasicAuthentication(elasticOptions.Value.User, elasticOptions.Value.Key));

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

            var pipelines = new List<string>();

            var cognitiveSearchEnabled = _settingsManager.GetConginiteSearchEnabled();
            if (cognitiveSearchEnabled)
            {
                // check if ml field is created
                await CreateMLField(createIndexResult.IndexName);

                // check if ml pipleline created
                var pipelineName = _settingsManager.GetPiplelineName();

                await CheckMLPipeline(pipelineName);

                pipelines.Add(pipelineName);
            }

            var bulkResponse = await Client.BulkAsync(x => CreateBulkIndexRequest(createIndexResult.IndexName, createIndexResult.ProviderDocuments, x, pipelines));

            await Client.Indices.RefreshAsync(Indices.Index(createIndexResult.IndexName));

            if (!bulkResponse.IsValidResponse)
            {
                result.Items.Add(new IndexingResultItem
                {
                    Id = ModuleConstants.ElasticSearchExceptionTitle,
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

        private async Task CheckMLPipeline(string pipelineName)
        {
            var getPipelineRequest = new GetPipelineRequest(pipelineName)
            {
                Summary = true
            };

            var piplineResult = await Client.Ingest.GetPipelineAsync(getPipelineRequest);
            if (piplineResult.ApiCallDetails.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                throw new SearchException($"ML pipeline is not found: {pipelineName}. Please created the pipeleine first.");
            }
        }

        private async Task CreateMLField(string indexName)
        {
            var fieldName = _settingsManager.GetModelFieldName();
            var propertyName = fieldName.Split('.').FirstOrDefault();

            var indexMappings = await GetMappingAsync(indexName);

            if (!indexMappings.ContainsKey(propertyName))
            {
                var rankFeaturesProperty = new Properties
                {
                    { fieldName, new RankFeaturesProperty() }
                };

                var request = new PutMappingRequest(indexName) { Properties = rankFeaturesProperty };
                var response = await Client.Indices.PutMappingAsync(request);

                if (!response.ApiCallDetails.HasSuccessfulStatusCode)
                {
                    throw new SearchException($"Failed to create ML field: {fieldName}", response.ApiCallDetails.OriginalException);
                }
            }
        }

        private static void CreateBulkIndexRequest(string indexName, IList<SearchDocument> documents, BulkRequestDescriptor descriptor, List<string> pipelines)
        {
            descriptor
                .Index(indexName)
                .IndexMany(documents);

            foreach (var pipeline in pipelines)
            {
                descriptor.Pipeline(pipeline);
            }
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

                    //todo: fix object serialization
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
            //.Aliases(x => ConfigureAliases(x, alias))
            );

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
            descriptor.Custom("lowercase", ConfigureLowerCaseNormaliser);
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

        protected virtual void ConfigureLowerCaseNormaliser(CustomNormalizerDescriptor descriptor)
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
                          mappingResponse.Indices.Values.FirstOrDefault()?.Mappings;

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

        public async Task DeleteIndexAsync(string documentType)
        {
            try
            {
                //get backup index by alias and delete if present
                var indexName = GetIndexName(documentType);

                if (indexName != null)
                {
                    var response = await Client.Indices.DeleteAsync(indexName);
                    if (!response.IsValidResponse && response.ApiCallDetails.HttpStatusCode != (int)HttpStatusCode.NotFound)
                    {
                        throw new SearchException(response.DebugInformation);
                    }
                }

                RemoveMappingFromCache(indexName);
            }
            catch (Exception ex)
            {
                ThrowException("Failed to delete index", ex);
            }
        }

        public async Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            var indexName = GetIndexName(documentType);

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

        private static void CreateBulkDeleteRequest(string indexName, IList<SearchDocument> documents, BulkRequestDescriptor descriptor)
        {
            var ids = documents.Select(x => new Id(x.Id));

            descriptor.DeleteMany(indexName, ids);
        }

        protected virtual string GetIndexName(string documentType)
        {
            return string.Join("-", _searchOptions.GetScope(documentType), documentType).ToLowerInvariant();
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out _);
        }

        protected class CreateIndexResult
        {
            public string IndexName { get; set; }

            public IList<SearchDocument> ProviderDocuments { get; set; }
        }
    }
}
