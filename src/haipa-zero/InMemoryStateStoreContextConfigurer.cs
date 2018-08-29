using HyperVPlus.StateDb;
using Microsoft.EntityFrameworkCore;

namespace Haipa.Runtime.Zero
{
    internal class InMemoryStateStoreContextConfigurer : IDbContextConfigurer<StateStoreContext> 
    {
        public void Configure(DbContextOptionsBuilder options)
        {
            options.UseInMemoryDatabase("StateDb");
        }
    }
}