using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules;

namespace Haipa.Configuration
{
    // ReSharper disable once UnusedTypeParameter
    public interface IConfigSeeder<TModule> where TModule : IModule
    {
        Task Execute(CancellationToken stoppingToken);
    }
}