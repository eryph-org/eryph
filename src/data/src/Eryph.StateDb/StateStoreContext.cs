using System;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb
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
        public DbSet<OperationResource> OperationResources { get; set; }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<VirtualMachine> VirtualMachines { get; set; }
        public DbSet<VMHostMachine> VMHosts { get; set; }

        public DbSet<VirtualMachineNetworkAdapter> VirtualMachineNetworkAdapters { get; set; }
        public DbSet<VirtualMachineDrive> VirtualMachineDrives { get; set; }
        public DbSet<VirtualDisk> VirtualDisks { get; set; }

        //public DbSet<Network> Networks { get; set; }
        public DbSet<Subnet> Subnets { get; set; }

        public DbSet<MachineNetwork> MachineNetworks { get; set; }


        public DbSet<VirtualMachineMetadata> Metadata { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<Operation>().HasMany(c => c.LogEntries);
            modelBuilder.Entity<Operation>().HasMany(c => c.Tasks);
            modelBuilder.Entity<Operation>().HasMany(c => c.Resources);

            //modelBuilder.Entity<AgentNetwork>().HasKey("NetworkId", "AgentName");


            //modelBuilder.Entity<Agent>().HasKey(k => k.Name);
            //modelBuilder.Entity<Agent>().HasMany(c => c.Machines)
            //    .WithOne(one => one.Agent).HasForeignKey(fk => fk.AgentName);
            //modelBuilder.Entity<Agent>().HasMany(c => c.Networks)
            //.WithOne(one => one.Agent).HasForeignKey(fk => fk.AgentName);

            //modelBuilder.Entity<Network>().HasMany(c => c.Subnets)
            //    .WithOne(one => one.Network).HasForeignKey(fk => fk.NetworkId);

            modelBuilder.Entity<Machine>()
                .HasMany(x => x.Networks)
                .WithOne(x => x.Machine)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Machine>()
                .Navigation(x => x.Networks)
                .AutoInclude();

            modelBuilder.Entity<MachineNetwork>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<VirtualMachine>()
                .HasOne(x => x.Host)
                .WithMany(x => x.VMs);

            modelBuilder.Entity<VirtualMachine>()
                .HasMany(x => x.NetworkAdapters)
                .WithOne(x => x.Vm)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VirtualMachine>()
                .Navigation(x => x.NetworkAdapters)
                .AutoInclude();
            

            modelBuilder.Entity<VirtualMachine>()
                .HasMany(x => x.Drives)
                .WithOne(x => x.Vm)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VirtualMachine>()
                .Navigation(x => x.Drives)
                .AutoInclude();


            modelBuilder.Entity<Subnet>().HasKey(c => c.Id);
            modelBuilder.Entity<Subnet>().HasIndex(x => x.Address);

            modelBuilder.Entity<VirtualMachineNetworkAdapter>().HasKey("MachineId", "Id");


            modelBuilder.Entity<VirtualMachineDrive>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<VirtualMachineDrive>()
                .HasOne(x => x.AttachedDisk)
                .WithMany(x => x.AttachedDrives)
                .HasForeignKey(x => x.AttachedDiskId);


            modelBuilder.Entity<VirtualDisk>().HasKey(x => x.Id);
            modelBuilder.Entity<VirtualDisk>().HasOne(x => x.Parent)
                .WithMany(x => x.Childs)
                .HasForeignKey(x => x.ParentId);

            modelBuilder.Entity<VirtualMachineMetadata>()
                .HasKey(x => x.Id);


            //this is for SQLLite only
            //TODO: add to SQLLite Model builder extension like in https://github.com/dbosoft/SAPHub/blob/main/src/SAPHub.StateDb/SqlModelBuilder.cs
            modelBuilder.Entity<OperationLogEntry>().Property(e => e.Timestamp).HasConversion(
                dateTimeOffset => dateTimeOffset.UtcDateTime,
                dateTime => new DateTimeOffset(dateTime));
        }
    }
}