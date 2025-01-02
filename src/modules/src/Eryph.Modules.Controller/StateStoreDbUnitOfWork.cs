using System.Threading.Tasks;
using Dbosoft.Rebus;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.Modules.Controller;

[UsedImplicitly]
public sealed class StateStoreDbUnitOfWork : IRebusUnitOfWork
{
    private readonly StateStoreContext _dbContext;
    private readonly IDbContextTransaction _dbTransaction;

    public StateStoreDbUnitOfWork(StateStoreContext dbContext)
    {
        _dbContext = dbContext;
        _dbTransaction = dbContext.Database.BeginTransaction();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbTransaction.DisposeAsync();
    }

    public async Task Commit()
    {
        await _dbContext.SaveChangesAsync();
        await _dbTransaction.CommitAsync();
    }

    public void Dispose()
    {
        _dbTransaction.Dispose();
    }
}
