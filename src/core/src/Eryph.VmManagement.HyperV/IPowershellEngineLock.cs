using System.Threading;
using System.Threading.Tasks;

namespace Eryph.VmManagement;

public interface IPowershellEngineLock
{
    Task AcquireLockAsync(CancellationToken cancellationToken = default);

    void ReleaseLock();
}
