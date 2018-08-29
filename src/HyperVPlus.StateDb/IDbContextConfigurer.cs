using Microsoft.EntityFrameworkCore;

namespace HyperVPlus.StateDb
{
    public interface IDbContextConfigurer<TContext> where TContext : DbContext
    {
        void Configure(DbContextOptionsBuilder options);
    }


}