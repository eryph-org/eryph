using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Dbosoft.Hosuto.Modules;
using Dbosoft.Hosuto.Modules.Hosting;
using Eryph.Modules.ComputeApi.Tests.Integration.Endpoints;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.Hosting;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

/// <summary>
/// Factory for bootstrapping an application in memory for functional end to end tests.
/// </summary>
/// <typeparam name="TModule">A type in the entry point assembly of the application.
/// Typically the Startup or Program classes can be used.</typeparam>
public class WebModuleFactory<TModule> : IDisposable where TModule : class, IWebModule
{
    private bool _disposed;
    private TestServer _server;
    private IHost _host;
    private Action<IWebHostBuilder> _configuration;
    private readonly IList<HttpClient> _clients = new List<HttpClient>();
    private readonly List<WebModuleFactory<TModule>> _derivedFactories =
        new List<WebModuleFactory<TModule>>();

    /// <summary>
    /// <para>
    /// Creates an instance of <see cref="WebModuleFactory{TModule}"/>. This factory can be used to
    /// create a <see cref="TestServer"/> instance using the MVC application defined by <typeparamref name="TEntryPoint"/>
    /// and one or more <see cref="HttpClient"/> instances used to send <see cref="HttpRequestMessage"/> to the <see cref="TestServer"/>.
    /// The <see cref="WebModuleFactory{TModule}"/> will find the entry point class of <typeparamref name="TEntryPoint"/>
    /// assembly and initialize the application by calling <c>IWebHostBuilder CreateWebHostBuilder(string [] args)</c>
    /// on <typeparamref name="TEntryPoint"/>.
    /// </para>
    /// <para>
    /// This constructor will infer the application content root path by searching for a
    /// <see cref="WebApplicationFactoryContentRootAttribute"/> on the assembly containing the functional tests with
    /// a key equal to the <typeparamref name="TEntryPoint"/> assembly <see cref="Assembly.FullName"/>.
    /// In case an attribute with the right key can't be found, <see cref="WebModuleFactory{TModule}"/>
    /// will fall back to searching for a solution file (*.sln) and then appending <typeparamref name="TEntryPoint"/> assembly name
    /// to the solution directory. The application root directory will be used to discover views and content files.
    /// </para>
    /// <para>
    /// The application assemblies will be loaded from the dependency context of the assembly containing
    /// <typeparamref name="TEntryPoint" />. This means that project dependencies of the assembly containing
    /// <typeparamref name="TEntryPoint" /> will be loaded as application assemblies.
    /// </para>
    /// </summary>
    public WebModuleFactory()
    {
        _configuration = ConfigureWebHost;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="WebModuleFactory{TModule}"/> class.
    /// </summary>
    ~WebModuleFactory()
    {
        Dispose(false);
    }

    /// <summary>
    /// Gets the <see cref="TestServer"/> created by this <see cref="WebModuleFactory{TModule}"/>.
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
    /// Gets the <see cref="IServiceProvider"/> created by the server associated with this <see cref="WebModuleFactory{TModule}"/>.
    /// </summary>
    public virtual IServiceProvider Services
    {
        get
        {
            EnsureServer();
            return _host?.Services ?? _server.Host.Services;
        }
    }

    /// <summary>
    /// Gets the <see cref="IReadOnlyList{WebApplicationFactory}"/> of factories created from this factory
    /// by further customizing the <see cref="IWebHostBuilder"/> when calling 
    /// <see cref="WebModuleFactory{TModule}.WithWebHostBuilder(Action{IWebHostBuilder})"/>.
    /// </summary>
    public IReadOnlyList<WebModuleFactory<TModule>> Factories => _derivedFactories.AsReadOnly();

    /// <summary>
    /// Gets the <see cref="WebModuleFactoryClientOptions"/> used by <see cref="CreateClient()"/>.
    /// </summary>
    public WebModuleFactoryClientOptions ClientOptions { get; private set; } = new WebModuleFactoryClientOptions();

    /// <summary>
    /// Creates a new <see cref="WebModuleFactory{TModule}"/> with a <see cref="IWebHostBuilder"/>
    /// that is further customized by <paramref name="configuration"/>.
    /// </summary>
    /// <param name="configuration">
    /// An <see cref="Action{IWebHostBuilder}"/> to configure the <see cref="IWebHostBuilder"/>.
    /// </param>
    /// <returns>A new <see cref="WebModuleFactory{TModule}"/>.</returns>
    public WebModuleFactory<TModule> WithWebHostBuilder(Action<IWebHostBuilder> configuration) =>
        WithWebHostBuilderCore(configuration);

    internal virtual WebModuleFactory<TModule> WithWebHostBuilderCore(Action<IWebHostBuilder> configuration)
    {
        var factory = new DelegatedWebApplicationFactory(
            ClientOptions,
            CreateServer,
            CreateHost,
            CreateModuleHostBuilder,
            GetTestAssemblies,
            ConfigureClient,
            builder =>
            {
                _configuration(builder);
                configuration(builder);
            },
            ConfigureModule);

        _derivedFactories.Add(factory);

        return factory;
    }

    public WebModuleFactory<TModule> WithModuleConfiguration(Action<IModuleHostingOptions> options)
    {
        var factory = new DelegatedWebApplicationFactory(
            ClientOptions,
            CreateServer,
            CreateHost,
            CreateModuleHostBuilder,
            GetTestAssemblies,
            ConfigureClient,
            ConfigureWebHost,
            o =>
            {
                ConfigureModule(o);
                options(o);
            });

        _derivedFactories.Add(factory);

        return factory;
    }

    public WebModuleFactory<TModule> WithModuleHostBuilder(Action<IModulesHostBuilder> configure)
    {
        var factory = new DelegatedWebApplicationFactory(
            ClientOptions,
            CreateServer,
            CreateHost,
            () =>
            {
                var builder = CreateModuleHostBuilder();
                configure(builder);
                return builder;
            },
            GetTestAssemblies,
            ConfigureClient,
            ConfigureWebHost,
            ConfigureModule);

        _derivedFactories.Add(factory);

        return factory;
    }


    private void EnsureServer()
    {
        if (_server != null)
        {
            return;
        }

        var hostBuilder = CreateModuleHostBuilder();
        hostBuilder.HostModule<TModule>(options =>
        {
#if NETSTANDARD
                options.BuildWebHostCallback(webHostBuilder =>
                {
                    _server = new TestServer(webHostBuilder);
                    return _server.Host;
                });
#else
            options.Configure(sp =>
            {
                _server = (TestServer)sp.GetRequiredService<IServer>();
            });
#endif
            ConfigureModule(options);
        });
#if NETSTANDARD
            hostBuilder.UseAspNetCore(CreateWebHostBuilder, (_, webHostBuilder) =>
#else
        hostBuilder.UseAspNetCore((_, webHostBuilder) =>
#endif
        {
            SetContentRoot(webHostBuilder);
            _configuration(webHostBuilder);

#if !NETSTANDARD
            webHostBuilder.UseTestServer();
#endif

        });
        _host = CreateHost(hostBuilder);


    }

    private void SetContentRoot(IWebHostBuilder builder)
    {
        if (SetContentRootFromSetting(builder))
        {
            return;
        }

        var metadataAttributes = GetContentRootMetadataAttributes(
            typeof(TModule).Assembly.FullName,
            typeof(TModule).Assembly.GetName().Name);

        string contentRoot = null;
        for (var i = 0; i < metadataAttributes.Length; i++)
        {
            var contentRootAttribute = metadataAttributes[i];
            var contentRootCandidate = Path.Combine(
                AppContext.BaseDirectory,
                contentRootAttribute.ContentRootPath);

            var contentRootMarker = Path.Combine(
                contentRootCandidate,
                Path.GetFileName(contentRootAttribute.ContentRootTest));

            if (File.Exists(contentRootMarker))
            {
                contentRoot = contentRootCandidate;
                break;
            }
        }

        if (contentRoot != null)
        {
            builder.UseContentRoot(contentRoot);
        }
        else
        {
            builder.UseSolutionRelativeContentRoot(typeof(TModule).Assembly.GetName().Name);
        }
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
    protected virtual IWebHostBuilder CreateWebHostBuilder()
    {
        return new WebHostBuilder();
    }

    private static bool SetContentRootFromSetting(IWebHostBuilder builder)
    {
        // Attempt to look for TEST_CONTENTROOT_APPNAME in settings. This should result in looking for
        // ASPNETCORE_TEST_CONTENTROOT_APPNAME environment variable.
        var assemblyName = typeof(TModule).Assembly.GetName().Name;
        var settingSuffix = assemblyName.ToUpperInvariant().Replace(".", "_");
        var settingName = $"TEST_CONTENTROOT_{settingSuffix}";

        var settingValue = builder.GetSetting(settingName);
        if (settingValue == null)
        {
            return false;
        }

        builder.UseContentRoot(settingValue);
        return true;
    }

    private WebApplicationFactoryContentRootAttribute[] GetContentRootMetadataAttributes(
        string tEntryPointAssemblyFullName,
        string tEntryPointAssemblyName)
    {
        var testAssembly = GetTestAssemblies();
        var metadataAttributes = testAssembly
            .SelectMany(a => CustomAttributeExtensions.GetCustomAttributes<WebApplicationFactoryContentRootAttribute>((Assembly)a))
            .Where(a => string.Equals(a.Key, tEntryPointAssemblyFullName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Key, tEntryPointAssemblyName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.Priority)
            .ToArray();

        return metadataAttributes;
    }

    /// <summary>
    /// Gets the assemblies containing the functional tests. The
    /// <see cref="WebApplicationFactoryContentRootAttribute"/> applied to these
    /// assemblies defines the content root to use for the given
    /// <typeparamref name="TEntryPoint"/>.
    /// </summary>
    /// <returns>The list of <see cref="Assembly"/> containing tests.</returns>
    protected virtual IEnumerable<Assembly> GetTestAssemblies()
    {
        try
        {
            // The default dependency context will be populated in .net core applications.
            var context = DependencyContext.Default;
            if (context == null || context.CompileLibraries.Count == 0)
            {
                // The app domain friendly name will be populated in full framework.
                return new[] { Assembly.Load(AppDomain.CurrentDomain.FriendlyName) };
            }

            var runtimeProjectLibraries = context.RuntimeLibraries
                .ToDictionary(r => r.Name, r => r, StringComparer.Ordinal);

            // Find the list of projects
            var projects = context.CompileLibraries.Where(l => l.Type == "project");

            var entryPointAssemblyName = typeof(TModule).Assembly.GetName().Name;

            // Find the list of projects referencing TEntryPoint.
            var candidates = context.CompileLibraries
                .Where(library => library.Dependencies.Any(d => string.Equals(d.Name, entryPointAssemblyName, StringComparison.Ordinal)));

            var testAssemblies = new List<Assembly>();
            foreach (var candidate in candidates)
            {
                if (runtimeProjectLibraries.TryGetValue(candidate.Name, out var runtimeLibrary))
                {
                    var runtimeAssemblies = runtimeLibrary.GetDefaultAssemblyNames(context);
                    testAssemblies.AddRange(runtimeAssemblies.Select(Assembly.Load));
                }
            }

            return testAssemblies;
        }
        catch (Exception)
        {
        }

        return Array.Empty<Assembly>();
    }


    /// <summary>
    /// Creates a <see cref="IHostBuilder"/> used to set up <see cref="TestServer"/>.
    /// </summary>
    /// <remarks>
    /// The default implementation of this method looks for a <c>public static IHostBuilder CreateHostBuilder(string[] args)</c>
    /// method defined on the entry point of the assembly of <typeparamref name="TModule" /> and invokes it passing an empty string
    /// array as arguments.
    /// </remarks>
    /// <returns>A <see cref="IHostBuilder"/> instance.</returns>
    protected virtual IModulesHostBuilder CreateModuleHostBuilder()
    {
        var hostBuilder = new ModulesHostBuilder();

#if NETSTANDARD
            hostBuilder.UseEnvironment(EnvironmentName.Development);
#else
        hostBuilder.UseEnvironment(Environments.Development);
#endif
        return hostBuilder;
    }

    /// <summary>
    /// Creates the <see cref="TestServer"/> with the bootstrapped application in <paramref name="builder"/>.
    /// This is only called for applications using <see cref="IWebHostBuilder"/>. Applications based on
    /// <see cref="IHostBuilder"/> will use <see cref="CreateHost"/> instead.
    /// </summary>
    /// <param name="builder">The <see cref="IWebHostBuilder"/> used to
    /// create the server.</param>
    /// <returns>The <see cref="TestServer"/> with the bootstrapped application.</returns>
    protected virtual TestServer CreateServer(IWebHostBuilder builder) => new TestServer(builder);

    /// <summary>
    /// Creates the <see cref="IHost"/> with the bootstrapped application in <paramref name="builder"/>.
    /// This is only called for applications using <see cref="IHostBuilder"/>. Applications based on
    /// <see cref="IWebHostBuilder"/> will use <see cref="CreateServer"/> instead.
    /// </summary>
    /// <param name="builder">The <see cref="IHostBuilder"/> used to create the host.</param>
    /// <returns>The <see cref="IHost"/> with the bootstrapped application.</returns>
    protected virtual IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        host.Start();
        return host;
    }

    /// <summary>
    /// Gives a fixture an opportunity to configure the module.
    /// </summary>
    /// <param name="builder">The <see cref="IWebHostBuilder"/> for the application.</param>
    protected virtual void ConfigureModule(IModuleHostingOptions options)
    {
    }

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
    /// Configures <see cref="HttpClient"/> instances created by this <see cref="WebModuleFactory{TModule}"/>.
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
            _host?.Dispose();
        }

        _disposed = true;
    }

    private class DelegatedWebApplicationFactory : WebModuleFactory<TModule>
    {
        private readonly Func<IWebHostBuilder, TestServer> _createServer;
        private readonly Func<IHostBuilder, IHost> _createHost;
        private readonly Func<IModulesHostBuilder> _createModuleHostBuilder;
        private readonly Func<IEnumerable<Assembly>> _getTestAssemblies;
        private readonly Action<HttpClient> _configureClient;
        private readonly Action<IModuleHostingOptions> _configureModule;

        public DelegatedWebApplicationFactory(
            WebModuleFactoryClientOptions options,
            Func<IWebHostBuilder, TestServer> createServer,
            Func<IHostBuilder, IHost> createHost,
            Func<IModulesHostBuilder> createModuleHostBuilder,
            Func<IEnumerable<Assembly>> getTestAssemblies,
            Action<HttpClient> configureClient,
            Action<IWebHostBuilder> configureWebHost,
            Action<IModuleHostingOptions> configureModule)
        {
            ClientOptions = new WebModuleFactoryClientOptions(options);
            _createServer = createServer;
            _createHost = createHost;
            _createModuleHostBuilder = createModuleHostBuilder;
            _getTestAssemblies = getTestAssemblies;
            _configureClient = configureClient;
            _configuration = configureWebHost;
            _configureModule = configureModule;
        }

        protected override TestServer CreateServer(IWebHostBuilder builder) => _createServer(builder);

        protected override IHost CreateHost(IHostBuilder builder) => _createHost(builder);

        protected override IModulesHostBuilder CreateModuleHostBuilder() => _createModuleHostBuilder();

        protected override IEnumerable<Assembly> GetTestAssemblies() => _getTestAssemblies();

        protected override void ConfigureWebHost(IWebHostBuilder builder) => _configuration(builder);

        protected override void ConfigureModule(IModuleHostingOptions options) => _configureModule(options);

        protected override void ConfigureClient(HttpClient client) => _configureClient(client);

        internal override WebModuleFactory<TModule> WithWebHostBuilderCore(Action<IWebHostBuilder> configuration)
        {
            return new DelegatedWebApplicationFactory(
                ClientOptions,
                _createServer,
                _createHost,
                _createModuleHostBuilder,
                _getTestAssemblies,
                _configureClient,
                builder =>
                {
                    _configuration(builder);
                    configuration(builder);
                },
                _configureModule);
        }
    }
}