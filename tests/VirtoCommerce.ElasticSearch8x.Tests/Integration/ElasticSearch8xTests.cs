using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using VirtoCommerce.ElasticSearch8x.Core.Models;
using VirtoCommerce.ElasticSearch8x.Data.Services;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.ElasticSearch8x.Tests.Integration
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class ElasticSearch8xTests : SearchProviderTests
    {
        private readonly IConfiguration _configuration;

        public ElasticSearch8xTests(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override ISearchProvider GetSearchProvider()
        {
            var searchOptions = Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8x" });
            var elasticOptions = Options.Create(_configuration.GetSection("ElasticSearch8x").Get<ElasticSearch8xOptions>());
            elasticOptions.Value.Server ??= Environment.GetEnvironmentVariable("TestElasticsearchHost") ?? "localhost:9200";

            var settingsManager = GetSettingsManager();

            var filtersBuilder = new ElasticSearchFiltersBuilder();
            var aggregationsBuilder = new ElasticSearchAggregationsBuilder(filtersBuilder);
            var requestBuilder = new ElasticSearchRequestBuilder(filtersBuilder, aggregationsBuilder, settingsManager);

            var responseBuilder = new ElasticSearchResponseBuilder();
            var propertyService = new ElasticSearchPropertyService();

            var provider = new ElasticSearch8xProvider(
                searchOptions,
                elasticOptions,
                settingsManager,
                requestBuilder,
                responseBuilder,
                propertyService
                );

            return provider;
        }
    }
}
