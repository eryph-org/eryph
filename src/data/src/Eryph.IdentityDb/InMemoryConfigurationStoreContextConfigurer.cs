using Dbosoft.IdentityServer.EfCore.Storage.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.IdentityDb
{
    public class InMemoryConfigurationStoreContextConfigurer : IDbContextConfigurer<ConfigurationDbContext>
    {
        private readonly InMemoryDatabaseRoot _dbRoot;

        public InMemoryConfigurationStoreContextConfigurer(InMemoryDatabaseRoot dbRoot)
        {
            _dbRoot = dbRoot;
        }

        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("ConfigurationStoreDb", _dbRoot);
        }
    }
}