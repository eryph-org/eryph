using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using Container = SimpleInjector.Container;

namespace Haipa.Modules
{
    public abstract class ModuleBase : IModule
    {
        public abstract string Name { get; }
        protected IHost Host;
        private readonly Container _container = new Container();

        protected static Action EmptyAction = () => { };

        protected Action ConfigureContainerWithServicesAction = EmptyAction;


        public virtual void Bootstrap(IServiceProvider serviceProvider)
        {
            ConfigureContainerWithServicesAction = () => ConfigureContainer(serviceProvider, _container);

            Host =  new HostBuilder()
                .ConfigureServices(sc => ConfigureServicesAndAppContainer(serviceProvider, sc))         
                .Build();

            _container.AutoCrossWireAspNetComponents(Host.Services);
            _container.Verify();

        }

        protected void ConfigureServicesAndAppContainer(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddSingleton(_container);

            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            WireUpServicesAndContainer(serviceProvider, services, _container);

            services.EnableSimpleInjectorCrossWiring(_container);
            ConfigureServices(serviceProvider, services);
            ConfigureContainerWithServicesAction();

            services.EnableSimpleInjectorCrossWiring(_container);

        }

        protected virtual void WireUpServicesAndContainer(IServiceProvider serviceProvider, IServiceCollection services, Container container)
        {
            
        }


        protected abstract void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services);


        protected virtual void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

        }

        public virtual Task Start()
        {
            return Host.StartAsync();
        }

        public virtual Task Stop()
        {
            var container = Host.Services.GetRequiredService<Container>();
            container.Dispose();

            return Host.StopAsync();
        }

        public virtual Task WaitForShutdownAsync()
        {
            return Host.WaitForShutdownAsync();
        }
    }
}
