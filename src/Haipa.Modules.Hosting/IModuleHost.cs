using System;
using System.Threading.Tasks;

namespace Haipa.Modules.Hosting
{
    public interface IModuleHost
    {
        void Bootstrap(IServiceProvider serviceProvider);
        Task Start();
        Task Stop();
        Task WaitForShutdownAsync();
    }

    public interface IModuleHost<TModule> : IModuleHost where TModule : IModule
    {
    }
}