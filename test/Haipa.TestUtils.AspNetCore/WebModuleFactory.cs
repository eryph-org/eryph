// original source and license:
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// based on https://github.com/aspnet/AspNetCore/blob/master/src/Mvc/Mvc.Testing/src/WebApplicationFactory.cs

using System;
using System.Collections.Generic;
using System.Net.Http;
using Haipa.Modules;
using Haipa.Modules.Hosting;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;

namespace Haipa.TestUtils
{

    /// <summary>
    /// Factory for bootstrapping an module in memory for functional end to end tests.
    /// </summary>
    /// <typeparam name="TModule">A module type.</typeparam>
    public class WebModuleFactory<TModule> : IDisposable where TModule : WebModuleBase
    {
        private bool _disposed;
        private TestServer _server;
        private Container _container;
        private Action<IWebHostBuilder> _configurationAction;
        protected Action<Container> ConfigureModuleContainerAction;

        private IList<HttpClient> _clients = new List<HttpClient>();
        private readonly List<WebModuleFactory<TModule>> _derivedFactories =
            new List<WebModuleFactory<TModule>>();

        /// <summary>
        /// <para>
        /// Creates an instance of <see cref="WebModuleFactory{TModule}"/>. This factory can be used to
        /// create a <see cref="TestServer"/> instance using the web module defined by <typeparamref name="TModule"/>
        /// and one or more <see cref="HttpClient"/> instances used to send <see cref="HttpRequestMessage"/> to the <see cref="TestServer"/>.
        /// The <see cref="WebModuleFactory{TEntryPoint}"/> will find the entry point class of <typeparamref name="TModule"/>
        /// assembly and initialize the application by calling <c>IWebHostBuilder CreateWebHostBuilder(string [] args)</c>
        /// on <typeparamref name="TModule"/>.
        /// </para>
        /// <para>
        /// The application assemblies will be loaded from the dependency context of the assembly containing
        /// <typeparamref name="TModule" />. This means that project dependencies of the assembly containing
        /// <typeparamref name="TModule" /> will be loaded as application assemblies.
        /// </para>
        /// </summary>
        public WebModuleFactory()
        {
            _container = new Container();
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            _configurationAction = ConfigureWebHost;
            ConfigureModuleContainerAction = ConfigureModuleContainer;
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="WebModuleFactory{TEntryPoint}"/> class.
        /// </summary>
        ~WebModuleFactory()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the <see cref="TestServer"/> created by this <see cref="WebModuleFactory{TEntryPoint}"/>.
        /// </summary>
        public TestServer Server
        {
            get
            {
                EnsureServer();
                return _server;
            }
        }

        /// <summary>
        /// Gets the <see cref="IReadOnlyList{WebApplicationFactory}"/> of factories created from this factory
        /// by further customizing the <see cref="IWebHostBuilder"/> when calling 
        /// <see cref="WebModuleFactory{TEntryPoint}.WithWebHostBuilder(Action{IWebHostBuilder})"/>.
        /// </summary>
        public IReadOnlyList<WebModuleFactory<TModule>> Factories => _derivedFactories.AsReadOnly();

        /// <summary>
        /// Gets the <see cref="WebApplicationFactoryClientOptions"/> used by <see cref="CreateClient()"/>.
        /// </summary>
        public WebModuleFactoryClientOptions ClientOptions { get; private set; } = new WebModuleFactoryClientOptions();

        /// <summary>
        /// Creates a new <see cref="WebModuleFactory{TModule}"/> with a <see cref="IWebHostBuilder"/>
        /// that is further customized by <paramref name="configuration"/>.
        /// </summary>
        /// <param name="configuration">
        /// An <see cref="Action{IWebHostBuilder}"/> to configure the <see cref="IWebHostBuilder"/>.
        /// </param>
        /// <returns>A new <see cref="WebModuleFactory{TEntryPoint}"/>.</returns>
        public WebModuleFactory<TModule> WithWebHostBuilder(Action<IWebHostBuilder> configuration) =>
            WithWebHostBuilderCore(configuration);

        internal virtual WebModuleFactory<TModule> WithWebHostBuilderCore(Action<IWebHostBuilder> configuration)
        {
            var factory = new DelegatedWebModuleFactory(
                ClientOptions,
                CreateServer,
                CreateWebHostBuilder,
                ConfigureClient,
                builder =>
                {
                    _configurationAction(builder);
                    configuration(builder);
                }, ConfigureModuleContainer);

            _derivedFactories.Add(factory);

            return factory;
        }

        private void EnsureServer()
        {
            if (_server != null)
            {
                return;
            }


            var serviceProvider = new Container();
            serviceProvider.HostModules().AddModule<TModule>();
            ConfigureModuleContainerAction(serviceProvider);

            var builder = CreateWebHostBuilder(serviceProvider);
            _configurationAction(builder);
            _server = CreateServer(builder);
        }

        /// <summary>
        /// Creates a <see cref="IWebHostBuilder"/> used to set up <see cref="TestServer"/>.
        /// </summary>
        /// <remarks>
        /// The default implementation of this method looks for a <c>public static IWebHostBuilder CreateWebHostBuilder(string[] args)</c>
        /// method defined on the entry point of the assembly of <typeparamref name="TModule" /> and invokes it passing an empty string
        /// array as arguments.
        /// </remarks>
        /// <returns>A <see cref="IWebHostBuilder"/> instance.</returns>
        protected virtual IWebHostBuilder CreateWebHostBuilder(IServiceProvider serviceProvider)
        {
            var module = serviceProvider.GetRequiredService<TModule>();
            var hostContext = new ModuleHostContext<TModule>(module, _container, serviceProvider);
            var builder = WebHost.CreateDefaultBuilder().AsModuleHost(hostContext);

            builder.UseEnvironment("Development");

            return builder;
        }

        /// <summary>
        /// Creates the <see cref="TestServer"/> with the bootstrapped module in <paramref name="builder"/>.
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
        /// Gives a fixture an opportunity to configure the module container before it used
        /// </summary>
        /// <param name="container">The <see cref="Container"/> for the module.</param>
        protected virtual void ConfigureModuleContainer(Container container)
        {
        }

        /// <summary>
        /// Creates an instance of <see cref="HttpClient"/> that automatically follows
        /// redirects and handles cookies.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        public HttpClient CreateClient() =>
            CreateClient(ClientOptions);

        /// <summary>
        /// Creates an instance of <see cref="HttpClient"/> that automatically follows
        /// redirects and handles cookies.
        /// </summary>
        /// <returns>The <see cref="HttpClient"/>.</returns>
        public HttpClient CreateClient(WebModuleFactoryClientOptions options) =>
            CreateDefaultClient(options.BaseAddress, options.CreateHandlers());

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
        /// Configures <see cref="HttpClient"/> instances created by this <see cref="WebModuleFactory{TEntryPoint}"/>.
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

                foreach (var factory in _derivedFactories)
                {
                    factory.Dispose();
                }

                _server?.Dispose();
            }

            _disposed = true;
        }

        private class DelegatedWebModuleFactory : WebModuleFactory<TModule>
        {
            private readonly Func<IWebHostBuilder, TestServer> _createServer;
            private readonly Func<IServiceProvider, IWebHostBuilder> _createWebHostBuilder;
            private readonly Action<HttpClient> _configureClient;

            public DelegatedWebModuleFactory(
                WebModuleFactoryClientOptions options,
                Func<IWebHostBuilder, TestServer> createServer,
                Func<IServiceProvider, IWebHostBuilder> createWebHostBuilder,
                Action<HttpClient> configureClient,
                Action<IWebHostBuilder> configureWebHost, 
                Action<Container> configureContainer)
            {
                ClientOptions = new WebModuleFactoryClientOptions(options);
                _createServer = createServer;
                _createWebHostBuilder = createWebHostBuilder;
                _configureClient = configureClient;
                ConfigureModuleContainerAction = configureContainer;
                _configurationAction = configureWebHost;
            }

            protected override TestServer CreateServer(IWebHostBuilder builder) => _createServer(builder);

            protected override IWebHostBuilder CreateWebHostBuilder(IServiceProvider sp) => _createWebHostBuilder(sp);

            protected override void ConfigureWebHost(IWebHostBuilder builder) => _configurationAction(builder);

            protected override void ConfigureClient(HttpClient client) => _configureClient(client);

            protected override void ConfigureModuleContainer(Container container) => ConfigureModuleContainerAction(container);

            internal override WebModuleFactory<TModule> WithWebHostBuilderCore(Action<IWebHostBuilder> configuration)
            {
                return new DelegatedWebModuleFactory(
                    ClientOptions,
                    _createServer,
                    _createWebHostBuilder,
                    _configureClient,
                    builder =>
                    {
                        _configurationAction(builder);
                        configuration(builder);
                    },
                    ConfigureModuleContainerAction);
            }
        }
    }
}