
using System.Threading.Tasks;
using Haipa.Modules.Abstractions;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Haipa.Modules.Api
{
    [UsedImplicitly]
    public class ApiModule : IWebModule
    {
        public string Name => "Haipa.Api";

        private IWebHost _webHost;
        private readonly Container _globalContainer;

        public ApiModule(Container globalContainer)
        {
            _globalContainer = globalContainer;
        }

        private void CreateWebHost()
        {
            _webHost = _globalContainer.GetInstance<IWebModuleHostBuilderFactory>().CreateWebHostBuilder(Name)
                .ConfigureServices(services =>
                    services.AddSingleton<IStartup>((sp) =>
                        new Startup(sp.GetService<IConfiguration>(), _globalContainer)))
                .Build();
        }
        public void Start()
        {          
            CreateWebHost();
            _webHost.Start();
        }

        public void Stop()
        {
            _webHost.Dispose();
        }

        public Task RunAsync()
        {
            CreateWebHost();
            return _webHost.RunAsync();
        }
    }


}
