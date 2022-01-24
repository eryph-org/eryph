using System.Threading.Tasks;
using Eryph.Rebus;
using Eryph.StateDb;
using JetBrains.Annotations;

namespace Eryph.Modules.Controller
{
    [UsedImplicitly]
    public class StateStoreDbUnitOfWork : IRebusUnitOfWork
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

        public Task Commit()
        {
            return _dbContext.SaveChangesAsync();
        }

        public void Dispose()
        {
        }
    }
}