using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Integration
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class ElasticSearch8Tests : SearchProviderTests
    {
        private readonly IConfiguration _configuration;

        public ElasticSearch8Tests(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override ISearchProvider GetSearchProvider()
        {
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8" });
            var elasticOptions = Options.Create(_configuration.GetSection("ElasticSearch8").Get<ElasticSearch8Options>());
            elasticOptions.Value.Server ??= Environment.GetEnvironmentVariable("TestElasticsearchHost") ?? "localhost:9200";

            var settingsManager = GetSettingsManager();

            var loggerFactory = LoggerFactory.Create(builder => { builder.ClearProviders(); });

            var filtersBuilder = new ElasticSearchFiltersBuilder();
            var aggregationsBuilder = new ElasticSearchAggregationsBuilder(filtersBuilder);
            var builderLogger = loggerFactory.CreateLogger<ElasticSearchRequestBuilder>();
            var requestBuilder = new ElasticSearchRequestBuilder(filtersBuilder, aggregationsBuilder, settingsManager, builderLogger);

            var responseBuilder = new ElasticSearchResponseBuilder();
            var propertyService = new ElasticSearchPropertyService();

            var providerLogger = loggerFactory.CreateLogger<ElasticSearch8Provider>();

            var provider = new ElasticSearch8Provider(
                searchOptions,
                elasticOptions,
                settingsManager,
                requestBuilder,
                responseBuilder,
                propertyService,
                providerLogger
                );

            return provider;
        }
    }
}
