using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb
{
    public interface IDbContextConfigurer<TContext> where TContext : DbContext
    {
        void Configure(DbContextOptionsBuilder options);
    }
}