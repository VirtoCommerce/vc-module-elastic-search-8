using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.ElasticSearch8x.Tests.Integration;

namespace VirtoCommerce.ElasticSearch8x.Tests
{
    public class Startup
    {
        public static void ConfigureHost(IHostBuilder hostBuilder)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<ElasticSearch8xTests>()
                .AddEnvironmentVariables()
                .Build();

            hostBuilder.ConfigureHostConfiguration(builder => builder.AddConfiguration(configuration));
        }
    }
}
