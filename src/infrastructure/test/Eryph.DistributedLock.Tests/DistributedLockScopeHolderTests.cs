using Medallion.Threading;
using Moq;
using Xunit;

namespace Eryph.DistributedLock.Tests;

public class DistributedLockScopeHolderTests
{
    private readonly Mock<IDistributedLockProvider> _lockProviderMock;

    private readonly IDistributedLockScopeHolder _distributedLockManager;

    public DistributedLockScopeHolderTests()
    {
        _lockProviderMock = new();
        _distributedLockManager = new DistributedLockScopeHolder(_lockProviderMock.Object);
    }

    [Fact]
    public async Task AcquireLock_AcquiresLockOnlyOnce()
    {
        var timeOut = TimeSpan.FromSeconds(1);
        var handleMock = new Mock<IDistributedSynchronizationHandle>();
        var lockMock = new Mock<IDistributedLock>();
        
        _lockProviderMock.Setup(p => p.CreateLock("test-lock"))
            .Returns(lockMock.Object);

        lockMock.Setup(p => p.AcquireAsync(timeOut, It.IsAny<CancellationToken>()))
            .Returns(async (TimeSpan _, CancellationToken ct) =>
            {
                await Task.Delay(10, ct);
                return handleMock.Object;
            });

        var tasks = Enumerable.Repeat("test-lock", 5)
            .Select(n => _distributedLockManager.AcquireLock(n, timeOut).AsTask())
            .ToList();

        await Task.WhenAll(tasks);

        _lockProviderMock.Verify(p => p.CreateLock("test-lock"), Times.Once);
        lockMock.Verify(p => p.AcquireAsync(timeOut, It.IsAny<CancellationToken>()), Times.Once);
    }
}
