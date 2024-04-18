using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Configuration
{
    // ReSharper disable once UnusedTypeParameter
    public interface IConfigSeeder<TModule> where TModule : class
    {
        Task Execute(CancellationToken stoppingToken);
    }
}