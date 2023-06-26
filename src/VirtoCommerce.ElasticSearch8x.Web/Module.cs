using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ElasticSearch8x.Core;
using VirtoCommerce.ElasticSearch8x.Core.Models;
using VirtoCommerce.ElasticSearch8x.Core.Services;
using VirtoCommerce.ElasticSearch8x.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.ElasticSearch8x.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    private bool IsElastic8xEnabled
    {
        get
        {
            var provider = Configuration.GetValue<string>("Search:Provider");
            return provider.EqualsInvariant("ElasticSearch8x");
        }
    }

    public void Initialize(IServiceCollection serviceCollection)
    {
        if (IsElastic8xEnabled)
        {
            serviceCollection.Configure<ElasticSearch8xOptions>(Configuration.GetSection("Search:ElasticSearch8x"));
            serviceCollection.AddSingleton<ISearchProvider, ElasticSearch8xProvider>();

            serviceCollection.AddSingleton<IElasticSearchFiltersBuilder, ElasticSearchFiltersBuilder>();
            serviceCollection.AddSingleton<IElasticSearchAggregationsBuilder, ElasticSearchAggregationsBuilder>();

            serviceCollection.AddSingleton<IElasticSearchRequestBuilder, ElasticSearchRequestBuilder>();
            serviceCollection.AddSingleton<IElasticSearchResponseBuilder, ElasticSearchResponseBuilder>();
        }
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
    }

    public void Uninstall()

    {
        // Nothing to do here
    }
}
