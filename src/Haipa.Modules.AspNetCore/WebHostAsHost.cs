using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace Haipa.Modules
{
    internal class WebHostAsHost : IHost
    {
        private readonly IWebHost _webHost;

        public WebHostAsHost(IWebHost webHost)
        {
            _webHost = webHost;
        }

        public void Dispose()
        {
            _webHost.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _webHost.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return _webHost.StopAsync(cancellationToken);
        }

        public IServiceProvider Services => _webHost.Services;
    }
}