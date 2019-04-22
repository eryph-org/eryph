using Microsoft.EntityFrameworkCore;

namespace Haipa.IdentityDb
{
    public interface IDbContextConfigurer<TContext> where TContext : DbContext
    {
        void Configure(DbContextOptionsBuilder options);
    }


}