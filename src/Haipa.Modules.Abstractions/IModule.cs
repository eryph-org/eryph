using System;
using System.Threading.Tasks;
using SimpleInjector;

namespace Haipa.Modules.Abstractions
{
    public interface IModule
    {
        string Name { get; }
        void Start();
        void Stop();

    }


    public interface IWebModule : IModule
    {
        Task RunAsync();

    }
}
