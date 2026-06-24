using System.Threading;
using System.Threading.Tasks;

namespace Eryph.ModuleCore.ChangeTracking;

/// <summary>
/// Handles a detected change by exporting the affected state to the on-disk config mirror. One handler
/// per change type; invoked by <see cref="ChangeTrackingBackgroundService{TChange}"/>.
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public interface IChangeHandler<TChange>
{
    Task HandleChangeAsync(
        TChange change,
        CancellationToken cancellationToken = default);
}
