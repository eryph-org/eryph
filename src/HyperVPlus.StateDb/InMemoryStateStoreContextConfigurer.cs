using HyperVPlus.StateDb;
using Microsoft.EntityFrameworkCore;

namespace Haipa.Runtime.Zero
{
    public class InMemoryStateStoreContextConfigurer : IDbContextConfigurer<StateStoreContext> 
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("StateDb");
        }
    }
}