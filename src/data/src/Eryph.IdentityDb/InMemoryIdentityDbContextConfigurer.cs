using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.IdentityDb
{
    public class InMemoryIdentityDbContextConfigurer : IDbContextConfigurer<IdentityDbContext>
    {
        private readonly InMemoryDatabaseRoot _dbRoot;

        public InMemoryIdentityDbContextConfigurer(InMemoryDatabaseRoot dbRoot)
        {
            _dbRoot = dbRoot;
        }

        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("IdentityDb", _dbRoot);
        }
    }
}