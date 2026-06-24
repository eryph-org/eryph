using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Eryph.IdentityDb;

public class InMemoryIdentityDbContextConfigurer(InMemoryDatabaseRoot dbRoot) : IDbContextConfigurer<IdentityDbContext>
{
    public void Configure(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase("IdentityDb", dbRoot);
    }
}
