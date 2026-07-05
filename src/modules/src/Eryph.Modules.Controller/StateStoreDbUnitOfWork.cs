using System;
using System.Data;
using System.Threading.Tasks;
using Dbosoft.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.Modules.Controller;

[UsedImplicitly]
public sealed class StateStoreDbUnitOfWork(
    StateStoreContext dbContext)
    : IRebusUnitOfWork
{
    private IDbContextTransaction? _dbTransaction;

    public async Task Initialize()
    {
        // A message handler's transaction wraps the whole handler. On MariaDB (the split-runtime store)
        // the default REPEATABLE READ takes a consistent snapshot, so a row another process committed
        // after the snapshot is invisible for the rest of the handler — the split-runtime
        // "Operation not found" when the controller handles an operation the compute API just created.
        // ReadCommitted re-reads the latest committed data on each statement.
        //
        // SQLite (eryph-zero, one process) has no such cross-process window and only supports
        // Serializable/ReadUncommitted — asking for ReadCommitted throws — so it keeps its default there.
        _dbTransaction = IsSqlite
            ? await dbContext.Database.BeginTransactionAsync()
            : await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
    }

    private bool IsSqlite =>
        dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    public async Task Commit()
    {
        await dbContext.SaveChangesAsync();
        if (_dbTransaction is not null)
            await _dbTransaction.CommitAsync();
    }

    public async Task Rollback()
    {
        if (_dbTransaction is not null)
            await _dbTransaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbTransaction is not null)
            await _dbTransaction.DisposeAsync();
    }

    public void Dispose()
    {
        _dbTransaction?.Dispose();
    }
}
