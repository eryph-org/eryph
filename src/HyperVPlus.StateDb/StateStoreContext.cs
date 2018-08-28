using HyperVPlus.StateDb.Model;
using Microsoft.EntityFrameworkCore;

namespace HyperVPlus.StateDb
{
    public class StateStoreContext : DbContext
    {
        public StateStoreContext(DbContextOptions<StateStoreContext> options)
            : base(options)
        {
        }

        public DbSet<Operation> Operations { get; set; }
        public DbSet<OperationLog> Logs { get; set; }

        public DbSet<Machine> Machines { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Operation>().HasMany(c => c.LogEntries);
        }
    }
}