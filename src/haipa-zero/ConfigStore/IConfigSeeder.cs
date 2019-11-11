using System.Threading;
using System.Threading.Tasks;
using Haipa.Modules;

namespace Haipa.Runtime.Zero.ConfigStore
{
    // ReSharper disable once UnusedTypeParameter
    internal interface IConfigSeeder<TModule> where TModule : IModule
    {
        Task Execute(CancellationToken stoppingToken);
    }
}