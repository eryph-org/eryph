namespace Eryph.DistributedLock;

/// <summary>
/// This class holds locks for a DI scope. The locks are released
/// when this class is disposed.
/// </summary>
/// <remarks>
/// This class is intended to be used when handling Rebus messages.
/// As each Rebus message is handled in a dedicated DI scope, this
/// class essentially holds the locks until Rebus disposes the message
/// handlers and the scope. Especially, this class will be disposed
/// after the Rebus unit of work has been committed.
/// </remarks>
public interface IDistributedLockScopeHolder : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Acquires a lock with the given name. Throws <see cref="TimeoutException"/>
    /// when the lock cannot be acquired within the configured timeout.
    /// </summary>
    public ValueTask AcquireLock(string name, TimeSpan timeOut);
}
