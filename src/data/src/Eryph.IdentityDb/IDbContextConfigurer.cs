using Microsoft.EntityFrameworkCore;

namespace Eryph.IdentityDb
{
    public interface IDbContextConfigurer<TContext> where TContext : DbContext
    {
        void Configure(DbContextOptionsBuilder options);
    }
}