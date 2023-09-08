using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.ElasticSearch8.Tests.Integration;

namespace VirtoCommerce.ElasticSearch8.Tests
{
    public class Startup
    {
        public static void ConfigureHost(IHostBuilder hostBuilder)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets<ElasticSearch8Tests>()
                .AddEnvironmentVariables()
                .Build();

            hostBuilder.ConfigureHostConfiguration(builder => builder.AddConfiguration(configuration));
        }
    }
}
