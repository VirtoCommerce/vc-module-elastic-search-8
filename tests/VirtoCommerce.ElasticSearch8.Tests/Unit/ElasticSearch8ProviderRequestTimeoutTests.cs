using System;
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
    public class ElasticSearch8ProviderRequestTimeoutTests
    {
        [Fact]
        public void Constructor_DefaultOptions_BoundsRequestsByThirtySecondsAndBulkByTenMinutes()
        {
            var provider = new TestElasticSearch8Provider(new ElasticSearch8Options { Server = "http://localhost:9200" });

            provider.ClientRequestTimeout.Should().Be(TimeSpan.FromSeconds(30));
            provider.ConfiguredLongRunningRequestTimeout.Should().Be(TimeSpan.FromMinutes(10));
        }

        [Fact]
        public void Constructor_TimeoutsConfigured_UsesTheConfiguredValues()
        {
            var provider = new TestElasticSearch8Provider(new ElasticSearch8Options
            {
                Server = "http://localhost:9200",
                RequestTimeout = TimeSpan.FromSeconds(5),
                LongRunningRequestTimeout = TimeSpan.FromMinutes(2),
            });

            provider.ClientRequestTimeout.Should().Be(TimeSpan.FromSeconds(5));
            provider.ConfiguredLongRunningRequestTimeout.Should().Be(TimeSpan.FromMinutes(2));
        }

        private sealed class TestElasticSearch8Provider : ElasticSearch8Provider
        {
            public TestElasticSearch8Provider(ElasticSearch8Options elasticOptions)
                : base(
                    Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8" }),
                    Options.Create(elasticOptions),
                    Mock.Of<ISettingsManager>(),
                    Mock.Of<IElasticSearchRequestBuilder>(),
                    Mock.Of<IElasticSearchResponseBuilder>(),
                    Mock.Of<IElasticSearchDocumentConverter>(),
                    Mock.Of<ILogger<ElasticSearch8Provider>>(),
                    Mock.Of<IElasticSearchPropertyService>(),
                    Mock.Of<IDistributedLockService>())
            {
            }

            public TimeSpan? ClientRequestTimeout => Client.ElasticsearchClientSettings.RequestTimeout;

            public TimeSpan ConfiguredLongRunningRequestTimeout => LongRunningRequestTimeout;
        }
    }
}
