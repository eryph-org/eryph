using Haipa.StateDb.Model;
using Microsoft.EntityFrameworkCore;

namespace Haipa.StateDb
{
    public class StateStoreContext : DbContext
    {
        public StateStoreContext(DbContextOptions<StateStoreContext> options)
            : base(options)
        {
        }

        public DbSet<Operation> Operations { get; set; }
        public DbSet<OperationLogEntry> Logs { get; set; }
        public DbSet<OperationTask> OperationTasks { get; set; }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<VirtualMachine> VirtualMachines { get; set; }
        public DbSet<VirtualMachineNetworkAdapter> VirtualMachineNetworkAdapters { get; set; }

        public DbSet<Network> Networks { get; set; }
        public DbSet<Subnet> Subnets { get; set; }
        public DbSet<AgentNetwork> AgentNetworks { get; set; }

        public DbSet<Agent> Agents { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Operation>().HasMany(c => c.LogEntries);
            modelBuilder.Entity<Operation>().HasMany(c => c.Tasks);

            modelBuilder.Entity<AgentNetwork>().HasKey("NetworkId", "AgentName");


            modelBuilder.Entity<Agent>().HasKey(k => k.Name);
            modelBuilder.Entity<Agent>().HasMany(c => c.Machines)
                .WithOne(one => one.Agent).HasForeignKey(fk => fk.AgentName);
            modelBuilder.Entity<Agent>().HasMany(c => c.Networks)
                .WithOne(one => one.Agent).HasForeignKey(fk => fk.AgentName);

            modelBuilder.Entity<Network>().HasMany(c => c.Subnets)
                .WithOne(one => one.Network).HasForeignKey(fk => fk.NetworkId);
            modelBuilder.Entity<Network>().HasMany(c => c.AgentNetworks)
                .WithOne(one => one.Network).HasForeignKey(fk => fk.NetworkId);

            modelBuilder.Entity<Machine>()
                .HasOne(x => x.VM)
                .WithOne(x=>x.Machine)
                .HasForeignKey<VirtualMachine>(x=>x.Id)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Machine>()
                .HasMany(x => x.Networks)
                .WithOne(x=>x.Machine)
                .HasForeignKey(x=>x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MachineNetwork>()
                .HasKey("MachineId", "AdapterName");


            modelBuilder.Entity<VirtualMachine>()
                .HasMany(x => x.NetworkAdapters)
                .WithOne(x => x.Vm)
                .HasForeignKey(x=>x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subnet>().HasKey(c => c.Id);
            modelBuilder.Entity<Subnet>().HasIndex(x => x.Address);


            modelBuilder.Entity<VirtualMachineNetworkAdapter>().HasKey("MachineId", "Name");
        }
    }
}