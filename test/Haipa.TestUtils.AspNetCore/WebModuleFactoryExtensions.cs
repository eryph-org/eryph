using System;
using System.Collections.Generic;
using System.Net.Http;
using Haipa.Modules;
using Haipa.TestUtils.Handlers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Simple.OData.Client;

namespace Haipa.TestUtils
{
    public static class WebModuleFactoryExtensions
    {
        public static ODataClient CreateODataClient<T>(this WebModuleFactory<T> self, string uriString) where T : WebModuleBase
        {
            
            return new ODataClient(
                new ODataClientSettings(self.CreateAuthenticatedClient((s) =>
                    {

                    }),
                    new Uri(uriString, UriKind.Relative)));

        }

        public static ODataClient CreateODataClient<T>(this WebModuleFactory<T> self, string uriString, Action<IServiceCollection> services) where T : WebModuleBase
        {
            

            return new ODataClient(
                new ODataClientSettings(self.CreateAuthenticatedClient(services),
                    new Uri(uriString, UriKind.Relative)));

        }

        public static HttpClient CreateAuthenticatedClient<T>(this WebModuleFactory<T> self, Action<IServiceCollection> services) where T : WebModuleBase
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