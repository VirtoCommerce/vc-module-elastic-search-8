using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Mapping;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class ElasticSearch8ProviderCreateIndexLockTests
    {
        [Fact]
        public async Task InternalCreateIndexWithLockAsync_PreventsDuplicateIndexCreation()
        {
            // Arrange
            var indexStoreWithoutLock = new IndexStore(new Barrier(2));
            var providerWithoutLock = new TestElasticSearch8Provider(indexStoreWithoutLock);

            // Act
            await Task.WhenAll(
                providerWithoutLock.CallInternalCreateIndexAsync(),
                providerWithoutLock.CallInternalCreateIndexAsync());

            // Assert
            indexStoreWithoutLock.CreatedIndexCount.Should().Be(2, "without a lock both calls pass the index existence check before the index is created");

            var indexStoreWithLock = new IndexStore();
            var providerWithLock = new TestElasticSearch8Provider(indexStoreWithLock);

            await Task.WhenAll(
                providerWithLock.CallInternalCreateIndexWithLockAsync(),
                providerWithLock.CallInternalCreateIndexWithLockAsync());

            indexStoreWithLock.CreatedIndexCount.Should().Be(1, "the lock should serialize index creation and prevent duplicate indexes");
        }

        private sealed class TestElasticSearch8Provider : ElasticSearch8Provider
        {
            private const string DocumentType = "Product";
            private readonly IndexStore _indexStore;

            public TestElasticSearch8Provider(IndexStore indexStore)
                : base(
                    Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8" }),
                    Options.Create(new ElasticSearch8Options()),
                    Mock.Of<ISettingsManager>(),
                    Mock.Of<IElasticSearchRequestBuilder>(),
                    Mock.Of<IElasticSearchResponseBuilder>(),
                    Mock.Of<IElasticSearchDocumentConverter>(),
                    Mock.Of<ILogger<ElasticSearch8Provider>>(),
                    Mock.Of<IElasticSearchPropertyService>(),
                    new PassThroughDistributedLockService())
            {
                _indexStore = indexStore;
            }

            public Task CallInternalCreateIndexAsync()
            {
                return InternalCreateIndexAsync(DocumentType, [], new IndexingParameters());
            }

            public Task CallInternalCreateIndexWithLockAsync()
            {
                return InternalCreateIndexWithLockAsync(DocumentType, [], new IndexingParameters());
            }

            protected override Task<IDictionary<PropertyName, IProperty>> GetMappingAsync(string indexName)
            {
                return Task.FromResult<IDictionary<PropertyName, IProperty>>(new Dictionary<PropertyName, IProperty>());
            }

            protected override Task<bool> IndexExistsAsync(string indexName)
            {
                return _indexStore.IndexExistsAsync();
            }

            protected override Task CreateIndexAsync(string documentType, string indexName, string alias)
            {
                _indexStore.CreateIndex();

                return Task.CompletedTask;
            }

            protected override Task UpdateMappingAsync(string documentType, string indexName, Properties properties)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class IndexStore
        {
            private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
            private readonly Barrier _indexExistsBarrier;
            private int _createdIndexCount;

            public IndexStore(Barrier indexExistsBarrier = null)
            {
                _indexExistsBarrier = indexExistsBarrier;
            }

            public int CreatedIndexCount => Volatile.Read(ref _createdIndexCount);

            public async Task<bool> IndexExistsAsync()
            {
                var indexExists = CreatedIndexCount > 0;
                await Task.Yield();
                _indexExistsBarrier?.SignalAndWait(Timeout);

                return indexExists;
            }

            public void CreateIndex()
            {
                Interlocked.Increment(ref _createdIndexCount);
            }
        }

        private sealed class PassThroughDistributedLockService : IDistributedLockService
        {
            public T Execute<T>(string resourceKey, Func<T> resolver, TimeSpan? lockTimeout = null, TimeSpan? tryLockTimeout = null, TimeSpan? retryInterval = null, CancellationToken? cancellationToken = null)
            {
                return resolver();
            }

            public Task<T> ExecuteAsync<T>(string resourceKey, Func<Task<T>> resolver, TimeSpan? lockTimeout = null, TimeSpan? tryLockTimeout = null, TimeSpan? retryInterval = null, CancellationToken? cancellationToken = null)
            {
                return resolver();
            }
        }
    }
}
