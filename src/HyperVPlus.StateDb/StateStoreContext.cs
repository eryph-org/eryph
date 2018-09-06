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
        public DbSet<Agent> Agents { get; set; }
        public DbSet<IpV4Address> Ipv4Addresses { get; set; }
        public DbSet<IpV6Address> Ipv6Addresses { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Operation>().HasMany(c => c.LogEntries);
            modelBuilder.Entity<Agent>().HasKey(k => k.Name);
            modelBuilder.Entity<Agent>().HasMany(c => c.Machines)
                .WithOne(one => one.Agent).HasForeignKey(fk => fk.AgentName);

            modelBuilder.Entity<IpV4Address>().HasKey("MachineId", "Address");
            modelBuilder.Entity<IpV6Address>().HasKey("MachineId", "Address");

            modelBuilder.Entity<Machine>().HasMany(c => c.IpV4Addresses)
                .WithOne(one => one.Machine).HasForeignKey(k => k.MachineId);
            modelBuilder.Entity<Machine>().HasMany(c => c.IpV6Addresses)
                .WithOne(one => one.Machine).HasForeignKey(k => k.MachineId);
        }
    }
}