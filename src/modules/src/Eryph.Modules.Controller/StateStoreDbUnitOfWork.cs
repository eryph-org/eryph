using System.Threading.Tasks;
using Dbosoft.Rebus;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller
{
    [UsedImplicitly]
    public sealed class StateStoreDbUnitOfWork : IRebusUnitOfWork
    {
        private readonly StateStoreContext _dbContext;

        public StateStoreDbUnitOfWork(StateStoreContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        public Task Initialize()
        {
            return Task.CompletedTask;
        }

        public Task Commit()
        {
            return _dbContext.SaveChangesAsync();
        }

        public Task Rollback()
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}