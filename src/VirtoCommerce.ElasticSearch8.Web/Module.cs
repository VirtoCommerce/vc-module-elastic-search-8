using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ElasticSearch8.Core;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Extensions;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;

namespace VirtoCommerce.ElasticSearch8.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
        {
            serviceCollection.Configure<ElasticSearch8Options>(Configuration.GetSection($"Search:{ModuleConstants.ProviderName}"));
            serviceCollection.AddSingleton<ElasticSearch8Provider>();

            serviceCollection.AddSingleton<IElasticSearchFiltersBuilder, ElasticSearchFiltersBuilder>();
            serviceCollection.AddSingleton<IElasticSearchAggregationsBuilder, ElasticSearchAggregationsBuilder>();

            serviceCollection.AddSingleton<IElasticSearchRequestBuilder, ElasticSearchRequestBuilder>();
            serviceCollection.AddSingleton<IElasticSearchResponseBuilder, ElasticSearchResponseBuilder>();

            serviceCollection.AddSingleton<IElasticSearchPropertyService, ElasticSearchPropertyService>();
            serviceCollection.AddSingleton<IElasticSearchDocumentConverter, ElasticSearchDocumentConverter>();
        }
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

        if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
        {
            appBuilder.UseSearchProvider<ElasticSearch8Provider>(ModuleConstants.ProviderName, (provider, documentTypes) =>
            {
                _ = provider.AddActiveAlias(documentTypes);
            });
        }
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
