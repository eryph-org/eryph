using System;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
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

        public DbSet<Catlet> Catlets { get; set; }
        public DbSet<VirtualCatlet> VirtualCatlets { get; set; }
        public DbSet<VirtualCatletHost> VirtualCatletHosts { get; set; }

        public DbSet<VirtualCatletNetworkAdapter> VirtualCatletNetworkAdapters { get; set; }
        public DbSet<VirtualMachineDrive> VirtualCatletDrives { get; set; }
        public DbSet<VirtualDisk> VirtualDisks { get; set; }

        public DbSet<VirtualNetwork> VirtualNetworks { get; set; }

        public DbSet<NetworkPort> NetworkPorts { get; set; }

        public DbSet<VirtualNetworkPort> VirtualNetworkPorts { get; set; }
        public DbSet<CatletNetworkPort> CatletNetworkPorts { get; set; }
        public DbSet<ProviderRouterPort> ProviderNetworkPorts { get; set; }

        public DbSet<ProviderSubnet> ProviderSubnets { get; set; }
        public DbSet<VirtualNetworkSubnet> VirtualNetworkSubnets { get; set; }

        public DbSet<IpPool> IpPools { get; set; }
        public DbSet<IpPoolAssignment> IpPoolAssignments { get; set; }


        public DbSet<ReportedNetwork> ReportedNetworks { get; set; }

        public DbSet<VirtualMachineMetadata> Metadata { get; set; }
        public DbSet<Project> Projects { get; set; }

        public DbSet<Tenant> Tenants { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Operation>().HasMany(c => c.LogEntries);
            modelBuilder.Entity<Operation>().HasMany(c => c.Tasks);
            modelBuilder.Entity<Operation>().HasMany(c => c.Resources);



            modelBuilder.Entity<Tenant>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Tenant>()
                .HasMany(x => x.Projects)
                .WithOne(x => x.Tenant)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tenant>()
                .Navigation(x => x.Projects);

            modelBuilder.Entity<Project>()
                .HasKey(x=>x.Id);

            modelBuilder.Entity<Project>()
                .HasMany(x => x.Catlets)
                .WithOne(x => x.Project)
                .HasForeignKey(x=>x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .Navigation(x => x.Catlets);


            modelBuilder.Entity<Project>()
                .HasMany(x => x.VirtualNetworks)
                .WithOne(x => x.Project)
                .HasForeignKey(x=>x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Project>()
                .Navigation(x => x.VirtualNetworks);

            modelBuilder.Entity<Catlet>()
                .HasMany(x => x.ReportedNetworks)
                .WithOne(x => x.Catlet)
                .HasForeignKey(x => x.CatletId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Catlet>()
                .Navigation(x => x.ReportedNetworks);


            modelBuilder.Entity<Catlet>()
                .HasMany(x => x.NetworkPorts)
                .WithOne(x => x.Catlet)
                .HasForeignKey(x => x.CatletId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Catlet>()
                .Navigation(x => x.NetworkPorts);



            modelBuilder.Entity<VirtualNetwork>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<VirtualNetwork>()
                .HasMany(x => x.NetworkPorts)
                .WithOne(x => x.Network)
                .HasForeignKey(x => x.NetworkId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<NetworkRouterPort>()
                .HasOne(x => x.RoutedNetwork)
                .WithOne(x => x.RouterPort)
                .HasForeignKey<NetworkRouterPort>(x=>x.RoutedNetworkId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<VirtualNetwork>()
                .Navigation(x => x.NetworkPorts);


            modelBuilder.Entity<VirtualNetwork>()
                .HasMany(x => x.Subnets)
                .WithOne(x => x.Network)
                .HasForeignKey(x => x.NetworkId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VirtualNetwork>()
                .Navigation(x => x.Subnets);


            modelBuilder.Entity<NetworkPort>()
                .HasKey(x=>x.Id);

            modelBuilder.Entity<NetworkPort>()
                .HasMany(x => x.IpAssignments)
                .WithOne(x => x.NetworkPort)
                .HasForeignKey(x => x.NetworkPortId)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<NetworkPort>()
                .Navigation(x => x.IpAssignments);

            modelBuilder.Entity<NetworkPort>()
                .HasIndex(x => x.MacAddress)
                .IsUnique();


            modelBuilder.Entity<Subnet>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Subnet>()
                .HasMany(x => x.IpPools)
                .WithOne(x => x.Subnet)
                .HasForeignKey(x => x.SubnetId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Subnet>()
                .Navigation(x => x.IpPools);

            modelBuilder.Entity<IpPool>()
                .HasKey(x => x.Id);
            modelBuilder.Entity<IpPool>().Property(x => x.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<IpPool>()
                .HasMany(x => x.IpAssignments)
                .WithOne(x => x.Pool)
                .HasForeignKey(x => x.PoolId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<IpPool>()
                .Navigation(x => x.IpAssignments);

            modelBuilder.Entity<ReportedNetwork>()
                .HasKey(x => x.Id);


            modelBuilder.Entity<VirtualCatlet>()
                .HasOne(x => x.Host)
                .WithMany(x => x.VirtualCatlets);

            modelBuilder.Entity<VirtualCatlet>()
                .HasMany(x => x.NetworkAdapters)
                .WithOne(x => x.Vm)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VirtualCatlet>()
                .Navigation(x => x.NetworkAdapters)
                .AutoInclude();
            

            modelBuilder.Entity<VirtualCatlet>()
                .HasMany(x => x.Drives)
                .WithOne(x => x.Vm)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<VirtualCatlet>()
                .Navigation(x => x.Drives)
                .AutoInclude();

            modelBuilder.Entity<VirtualCatletNetworkAdapter>().HasKey("MachineId", "Id");


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