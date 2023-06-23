using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ElasticSearch8x.Data.Models;
using VirtoCommerce.ElasticSearch8x.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
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

            serviceCollection.AddSingleton<SearchRequestBuilder>();
            serviceCollection.AddSingleton<SearchResponseBuilder>();
        }
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {

    }

    public void Uninstall()

    {
        // Nothing to do here
    }
}
