using System.Threading;
using System.Threading.Tasks;

namespace Haipa.Modules
{
    public interface IModuleHandler
    {
        Task Execute(CancellationToken stoppingToken);
    }
}