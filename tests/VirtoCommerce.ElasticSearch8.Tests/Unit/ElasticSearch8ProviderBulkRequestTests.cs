using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.ElasticSearch8.Core.Models;
using VirtoCommerce.ElasticSearch8.Core.Services;
using VirtoCommerce.ElasticSearch8.Data.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.ElasticSearch8.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class ElasticSearch8ProviderBulkRequestTests
    {
        [Fact]
        public async Task RemoveAsync_BulkRequest_KeepsTheNdjsonContentType()
        {
            string bulkContentType = null;
            using var server = new LoopbackElasticServer(context =>
            {
                if (context.Request.Url!.AbsolutePath.EndsWith("/_bulk"))
                {
                    bulkContentType = context.Request.ContentType;
                    return RespondAsync(context, """{"took":1,"errors":false,"items":[]}""");
                }

                return RespondAsync(context, """{"_shards":{"total":1,"successful":1,"failed":0}}""");
            });
            var provider = CreateProvider(new ElasticSearch8Options { Server = server.Url });

            await provider.RemoveAsync("Product", [new IndexDocument("p-1")]);

            bulkContentType.Should().StartWith("application/x-ndjson");
        }

        [Fact]
        public async Task RemoveAsync_ServerNeverAnswers_FailsLongBeforeTheClientWideTimeout()
        {
            using var server = new LoopbackElasticServer(_ => Task.CompletedTask);
            var provider = CreateProvider(new ElasticSearch8Options
            {
                Server = server.Url,
                RequestTimeout = TimeSpan.FromSeconds(20),
                LongRunningRequestTimeout = TimeSpan.FromMilliseconds(200),
            });
            var stopwatch = Stopwatch.StartNew();

            var remove = () => provider.RemoveAsync("Product", [new IndexDocument("p-1")]);

            await remove.Should().ThrowAsync<SearchException>();
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
        }

        private static ElasticSearch8Provider CreateProvider(ElasticSearch8Options elasticOptions)
        {
            return new ElasticSearch8Provider(
                Options.Create(new SearchOptions { Scope = "test-core", Provider = "ElasticSearch8" }),
                Options.Create(elasticOptions),
                Mock.Of<ISettingsManager>(),
                Mock.Of<IElasticSearchRequestBuilder>(),
                Mock.Of<IElasticSearchResponseBuilder>(),
                Mock.Of<IElasticSearchDocumentConverter>(),
                Mock.Of<ILogger<ElasticSearch8Provider>>(),
                Mock.Of<IElasticSearchPropertyService>(),
                new PassThroughDistributedLock());
        }

        private static async Task RespondAsync(HttpListenerContext context, string json)
        {
            var body = Encoding.UTF8.GetBytes(json);
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Elastic-Product"] = "Elasticsearch";
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }

        private sealed class LoopbackElasticServer : IDisposable
        {
            private readonly HttpListener _listener = new();
            private readonly Func<HttpListenerContext, Task> _handler;

            public LoopbackElasticServer(Func<HttpListenerContext, Task> handler)
            {
                _handler = handler;
                Url = $"http://localhost:{GetFreePort()}/";
                _listener.Prefixes.Add(Url);
                _listener.Start();
                _ = AcceptAsync();
            }

            public string Url { get; }

            public void Dispose()
            {
                _listener.Close();
            }

            private async Task AcceptAsync()
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext context;
                    try
                    {
                        context = await _listener.GetContextAsync();
                    }
                    catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
                    {
                        return;
                    }

                    _ = _handler(context);
                }
            }

            private static int GetFreePort()
            {
                using var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();

                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
        }
    }
}
