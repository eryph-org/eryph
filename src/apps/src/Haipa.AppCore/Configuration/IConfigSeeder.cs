using System.Threading;
using System.Threading.Tasks;

namespace Haipa.Configuration
{
    // ReSharper disable once UnusedTypeParameter
    public interface IConfigSeeder<TModule> where TModule : class
    {
        Task Execute(CancellationToken stoppingToken);
    }
}