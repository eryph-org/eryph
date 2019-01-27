using Microsoft.EntityFrameworkCore;

namespace Haipa.StateDb
{
    public interface IDbContextConfigurer<TContext> where TContext : DbContext
    {
        void Configure(DbContextOptionsBuilder options);
    }


}