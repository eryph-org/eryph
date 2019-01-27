using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Haipa.StateDb
{
    public class InMemoryStateStoreContextConfigurer : IDbContextConfigurer<StateStoreContext> 
    {
        private readonly InMemoryDatabaseRoot _dbRoot;

        public InMemoryStateStoreContextConfigurer(InMemoryDatabaseRoot dbRoot)
        {
            _dbRoot = dbRoot;
        }

        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("StateDb", _dbRoot);
        }
    }
}