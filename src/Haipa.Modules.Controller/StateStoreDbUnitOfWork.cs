using System.Threading.Tasks;
using Haipa.Rebus;
using Haipa.StateDb;
using JetBrains.Annotations;

namespace Haipa.Modules.Controller
{

    [UsedImplicitly]
    public class StateStoreDbUnitOfWork : IRebusUnitOfWork
    {
        private readonly StateStoreContext _dbContext;

        public StateStoreDbUnitOfWork(StateStoreContext dbcontext)
        {
            _dbContext = dbcontext;
        }

        public ValueTask DisposeAsync()
        {
            return default;
        }

        public Task Commit()
        {
            return _dbContext.SaveChangesAsync();
        }
    }
}