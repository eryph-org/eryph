using System;
using System.Threading.Tasks;

namespace Haipa.Modules
{
    public interface IModule
    {
        string Name { get; }

        void Bootstrap(IServiceProvider serviceProvider);
        Task Start();
        Task Stop();
        Task WaitForShutdownAsync();

    }
}
