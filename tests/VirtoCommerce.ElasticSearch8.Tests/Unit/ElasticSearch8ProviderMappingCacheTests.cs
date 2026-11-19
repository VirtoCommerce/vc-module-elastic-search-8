using System.Collections.Generic;
using System.Linq;
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
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class ElasticSearch8ProviderMappingCacheTests
    {
        [Fact]
        public async Task GetMappingAsync_IndexCreatedAfterAnEarlierCall_LoadsTheMapping()
        {
            // Arrange - this instance searched before another instance created the index
            var provider = new TestElasticSearch8Provider { IndexExists = false };
            var mappingBeforeIndex = await provider.CallGetMappingAsync();
            provider.IndexExists = true;

            // Act
            var mapping = await provider.CallGetMappingAsync();

            // Assert
            mappingBeforeIndex.Should().BeEmpty();
            mapping.Keys.Select(x => x.Name).Should().Equal("modifieddate");
            provider.LoadCount.Should().Be(1);
        }

        [Fact]
        public async Task GetMappingAsync_IndexMappedAfterAnEarlierCall_LoadsTheMapping()
        {
            // Arrange - this instance searched between another instance's index creation and its mapping update
            var provider = new TestElasticSearch8Provider { IndexExists = true, Mapping = new Dictionary<PropertyName, IProperty>() };
            var mappingBeforeUpdate = await provider.CallGetMappingAsync();
            provider.Mapping = TestElasticSearch8Provider.ModifiedDateMapping();

            // Act
            var mapping = await provider.CallGetMappingAsync();

            // Assert
            mappingBeforeUpdate.Should().BeEmpty();
            mapping.Keys.Select(x => x.Name).Should().Equal("modifieddate");
            provider.LoadCount.Should().Be(2);
        }

        [Fact]
        public async Task GetMappingAsync_IndexExists_LoadsTheMappingOnce()
        {
            // Arrange
            var provider = new TestElasticSearch8Provider { IndexExists = true };

            // Act
            await provider.CallGetMappingAsync();
            var mapping = await provider.CallGetMappingAsync();

            // Assert
            mapping.Keys.Select(x => x.Name).Should().Equal("modifieddate");
            provider.LoadCount.Should().Be(1);
        }

        private sealed class TestElasticSearch8Provider : ElasticSearch8Provider
        {
            private const string IndexName = "test-core-auditlogrecord-active";

            public TestElasticSearch8Provider()
                : base(
                    Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8" }),
                    Options.Create(new ElasticSearch8Options()),
                    Mock.Of<ISettingsManager>(),
                    Mock.Of<IElasticSearchRequestBuilder>(),
                    Mock.Of<IElasticSearchResponseBuilder>(),
                    Mock.Of<IElasticSearchDocumentConverter>(),
                    Mock.Of<ILogger<ElasticSearch8Provider>>(),
                    Mock.Of<IElasticSearchPropertyService>(),
                    new PassThroughDistributedLock())
            {
            }

            public bool IndexExists { get; set; }

            public IDictionary<PropertyName, IProperty> Mapping { get; set; } = ModifiedDateMapping();

            public int LoadCount { get; private set; }

            public static IDictionary<PropertyName, IProperty> ModifiedDateMapping()
            {
                return new Dictionary<PropertyName, IProperty>
                {
                    ["modifieddate"] = new DateProperty(),
                };
            }

            public Task<IDictionary<PropertyName, IProperty>> CallGetMappingAsync()
            {
                return GetMappingAsync(IndexName);
            }

            protected override Task<bool> IndexExistsAsync(string indexName)
            {
                return Task.FromResult(IndexExists);
            }

            protected override Task<IDictionary<PropertyName, IProperty>> LoadMappingAsync(string indexName)
            {
                LoadCount++;

                return Task.FromResult(Mapping);
            }
        }
    }
}
