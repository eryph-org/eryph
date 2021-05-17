using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Hosuto.Modules;

namespace Haipa.Configuration
{
    // ReSharper disable once UnusedTypeParameter
    public interface IConfigSeeder<TModule> where TModule : class
    {
        Task Execute(CancellationToken stoppingToken);
    }
}