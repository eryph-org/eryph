using System.Threading;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.ChangeTracking;

internal interface IChangeHandler<TChange>
{
    Task HandleChangeAsync(
        TChange change,
        CancellationToken cancellationToken = default);
}