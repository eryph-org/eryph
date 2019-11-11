using IdentityServer4.EntityFramework.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Haipa.IdentityDb
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
