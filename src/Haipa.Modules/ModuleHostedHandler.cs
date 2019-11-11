using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SimpleInjector;

namespace Haipa.Modules
{
    internal class ModuleHostedHandler<TModuleHandler> : BackgroundService where TModuleHandler : class, IModuleHandler
    {
        private readonly Container _container;

        public ModuleHostedHandler(Container container)
        {
            _container = container;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return _container.GetInstance<TModuleHandler>().Execute(stoppingToken);
        }
    }
}