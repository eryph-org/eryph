using System.Threading.Tasks;
using Dbosoft.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
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
        _dbTransaction = await dbContext.Database.BeginTransactionAsync();
    }

    public async Task Commit()
    {
        await dbContext.SaveChangesAsync();
        if(_dbTransaction is not null)
            await _dbTransaction.CommitAsync();
    }

    public async Task Rollback()
    {
        if (_dbTransaction is not null)
            await _dbTransaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if(_dbTransaction is not null)
            await _dbTransaction.DisposeAsync();
    }

    public void Dispose()
    {
        _dbTransaction?.Dispose();
    }
}
