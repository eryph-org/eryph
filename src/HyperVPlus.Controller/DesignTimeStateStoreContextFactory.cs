using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HyperVPlus.StateDb.MySql
{
    [UsedImplicitly]
    public class DesignTimeStateStoreContextFactory : IDesignTimeDbContextFactory<StateStoreContext>
    {
        public StateStoreContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<StateStoreContext>();
            optionsBuilder.UseMySql();

            return new StateStoreContext(optionsBuilder.Options);
        }
    }
    
}