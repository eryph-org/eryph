using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Haipa.Modules;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SimpleInjector;

namespace Haipa.TestUtils.AspNetCore.Server
{
    /// <summary>
    /// Host for running a module in memory for functional end to end tests.
    /// </summary>
    /// <typeparam name="TModule">The module to be run.</typeparam>
    public class WebModuleTestHost<TModule> where TModule: WebModuleBase
    {
        private bool _disposed;
        private TestServer _server;
        private TModule _module;
        private Action<IWebHostBuilder> _configuration;
        private IList<HttpClient> _clients = new List<HttpClient>();
        private Container _container;


        /// <summary>
        /// Gets the <see cref="TestServer"/> created by this <see cref="WebApplicationFactory{TEntryPoint}"/>.
        /// </summary>
        public TestServer Server => _server;


        /// <summary>
        /// Gets the <see cref="WebApplicationFactoryClientOptions"/> used by <see cref="CreateClient()"/>.
        /// </summary>
        public WebApplicationFactoryClientOptions ClientOptions { get; private set; } = new WebApplicationFactoryClientOptions();

        private void EnsureServer()
        {
            if (_server != null)
            {
                return;
            }

            EnsureDepsFile();

            _container = new Container();
            _container.Register<TModule>();

            _module = _container.GetInstance<TModule>();
            //_module(_container);

            
            //_configuration(builder);
            //_server = CreateServer(builder);
        }

        private void EnsureDepsFile()
        {
            //if (typeof(TModule).Assembly.EntryPoint == null)
            //{
            //    throw new InvalidOperationException("invalid (typeof(TModule).Name));
            //}

            var depsFileName = $"{typeof(TModule).Assembly.GetName().Name}.deps.json";
            var depsFile = new FileInfo(Path.Combine(AppContext.BaseDirectory, depsFileName));
            if (!depsFile.Exists)
            {
                throw new InvalidOperationException($"deps file missing: {depsFile.FullName}, {Path.GetFileName(depsFile.FullName)} ");
            }
        }


        /// <summary>
        /// Creates the <see cref="TestServer"/> with the bootstrapped application in <paramref name="builder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebHostBuilder"/> used to
        /// create the server.</param>
        /// <returns>The <see cref="TestServer"/> with the bootstrapped application.</returns>
        protected virtual TestServer CreateServer(IWebHostBuilder builder) => new TestServer(builder);

        /// <summary>
        /// Gives a fixture an opportunity to configure the application before it gets built.
        /// </summary>
        /// <param name="builder">The <see cref="IWebHostBuilder"/> for the application.</param>
        protected virtual void ConfigureWebHost(IWebHostBuilder builder)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="HttpClient"/> that automatically follows
        /// redirects and handles cookies.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        //public HttpClient CreateClient() =>
        //    CreateClient(ClientOptions);

        /// <summary>
        /// Creates an instance of <see cref="HttpClient"/> that automatically follows
        /// redirects and handles cookies.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        //public HttpClient CreateClient(WebApplicationFactoryClientOptions options) =>
        //    CreateDefaultClient(options.BaseAddress, options.CreateHandlers());

        /// <summary>
        /// Creates a new instance of an <see cref="HttpClient"/> that can be used to
        /// send <see cref="HttpRequestMessage"/> to the server. The base address of the <see cref="HttpClient"/>
        /// instance will be set to <c>http://localhost</c>.
        /// </summary>
        /// <param name="handlers">A list of <see cref="DelegatingHandler"/> instances to set up on the
        /// <see cref="HttpClient"/>.</param>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        public HttpClient CreateDefaultClient(params DelegatingHandler[] handlers)
        {
            EnsureServer();

            HttpClient client;
            if (handlers == null || handlers.Length == 0)
            {
                client = _server.CreateClient();
            }
            else
            {
                for (var i = handlers.Length - 1; i > 0; i--)
                {
                    handlers[i - 1].InnerHandler = handlers[i];
                }

                var serverHandler = _server.CreateHandler();
                handlers[handlers.Length - 1].InnerHandler = serverHandler;

                client = new HttpClient(handlers[0]);
            }

            _clients.Add(client);

            ConfigureClient(client);

            return client;
        }

        /// <summary>
        /// Configures <see cref="HttpClient"/> instances created by this <see cref="WebApplicationFactory{TEntryPoint}"/>.
        /// </summary>
        /// <param name="client">The <see cref="HttpClient"/> instance getting configured.</param>
        protected virtual void ConfigureClient(HttpClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            client.BaseAddress = new Uri("http://localhost");
        }

        /// <summary>
        /// Creates a new instance of an <see cref="HttpClient"/> that can be used to
        /// send <see cref="HttpRequestMessage"/> to the server.
        /// </summary>
        /// <param name="baseAddress">The base address of the <see cref="HttpClient"/> instance.</param>
        /// <param name="handlers">A list of <see cref="DelegatingHandler"/> instances to set up on the
        /// <see cref="HttpClient"/>.</param>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        public HttpClient CreateDefaultClient(Uri baseAddress, params DelegatingHandler[] handlers)
        {
            var client = CreateDefaultClient(handlers);
            client.BaseAddress = baseAddress;

            return client;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true" /> to release both managed and unmanaged resources;
        /// <see langword="false" /> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {                

                foreach (var client in _clients)
                {
                    client.Dispose();
                }

                _server?.Dispose();
            }

            _disposed = true;
        }

    }

    public class TestingWebHostBuilderFactory : IWebModuleHostBuilderFactory
    {

        public IWebHostBuilder CreateWebHostBuilder(string moduleName, string path)
        {
            return WebHost.CreateDefaultBuilder();
        }
    }
}