using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.Startup;

public interface IStartupHandler
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}
