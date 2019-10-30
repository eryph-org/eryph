using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Simple.OData.Client;

namespace Haipa.TestUtils.AspNetCore.Server
{
    public static class WebApplicationFactoryExtensions
    {
        public static ODataClient CreateODataClient<T>(this WebApplicationFactory<T> self) where T : class
        {
            
            return new ODataClient(
                new ODataClientSettings(self.CreateAuthenticatedClient((s) =>
                    {

                    }),
                    new Uri("/odata", UriKind.Relative)));

        }

        public static ODataClient CreateODataClient<T>(this WebApplicationFactory<T> self, Action<IServiceCollection> services) where T : class
        {
            

            return new ODataClient(
                new ODataClientSettings(self.CreateAuthenticatedClient(services),
                    new Uri("/odata", UriKind.Relative)));

        }

        public static HttpClient CreateAuthenticatedClient<T>(this WebApplicationFactory<T> self, Action<IServiceCollection> services) where T : class
        {

            var serviceList = new Action<IServiceCollection>(services);
            
            var modifiedBuilder = self.WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.Add(new MemoryConfigurationSource()
                            {InitialData = new[] {new KeyValuePair<string, string>("unitTestingMode", "true")}});
                    });
                builder.ConfigureTestServices(serviceList);
            });

            modifiedBuilder.CreateClient();
            return new HttpClient(new TestServerHandler(modifiedBuilder.Server.CreateHandler()))
            {
                BaseAddress = modifiedBuilder.Server.BaseAddress
            };

        }
    }
}