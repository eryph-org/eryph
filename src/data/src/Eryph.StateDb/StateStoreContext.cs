using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Eryph.Core;
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


        public DbSet<OperationModel> Operations { get; set; }
        public DbSet<OperationLogEntry> Logs { get; set; }
        public DbSet<OperationTaskModel> OperationTasks { get; set; }
        public DbSet<TaskProgressEntry> TaskProgress { get; set; }
        public DbSet<OperationResourceModel> OperationResources { get; set; }

        public DbSet<Resource> Resources { get; set; }
        
        public DbSet<Catlet> Catlets { get; set; }

        public DbSet<CatletFarm> CatletFarms { get; set; }

        public DbSet<CatletNetworkAdapter> CatletNetworkAdapters { get; set; }
        public DbSet<CatletDrive> CatletDrives { get; set; }
        public DbSet<VirtualDisk> VirtualDisks { get; set; }

        public DbSet<VirtualNetwork> VirtualNetworks { get; set; }

        public DbSet<NetworkPort> NetworkPorts { get; set; }

        public DbSet<VirtualNetworkPort> VirtualNetworkPorts { get; set; }
        public DbSet<CatletNetworkPort> CatletNetworkPorts { get; set; }
        public DbSet<ProviderRouterPort> ProviderRouterPorts { get; set; }

        public DbSet<ProviderSubnet> ProviderSubnets { get; set; }
        public DbSet<VirtualNetworkSubnet> VirtualNetworkSubnets { get; set; }

        public DbSet<IpPool> IpPools { get; set; }
        public DbSet<IpPoolAssignment> IpPoolAssignments { get; set; }


        public DbSet<ReportedNetwork> ReportedNetworks { get; set; }

        public DbSet<CatletMetadata> Metadata { get; set; }
        public DbSet<Project> Projects { get; set; }

        public DbSet<ProjectRoleAssignment> ProjectRoles { get; set; }


        public DbSet<Tenant> Tenants { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OperationModel>().HasMany(c => c.LogEntries);
            modelBuilder.Entity<OperationModel>().HasMany(c => c.Tasks);
            modelBuilder.Entity<OperationModel>().HasMany(c => c.Resources);
            modelBuilder.Entity<OperationModel>().HasMany(c => c.Projects);

            modelBuilder.Entity<OperationModel>()
                .Property(x => x.TenantId)
                .HasDefaultValue(EryphConstants.DefaultTenantId);

            modelBuilder.Entity<OperationModel>()
                .Property(x => x.LastUpdated).IsConcurrencyToken();

            modelBuilder.Entity<OperationTaskModel>()
                .Property(x => x.LastUpdated).IsConcurrencyToken();


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
                .HasMany(x => x.Resources)
                .WithOne(x => x.Project)
                .HasForeignKey(x=>x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // TODO might be configured this way implicitly
            modelBuilder.Entity<Project>()
                .HasMany(x => x.ProjectRoles)
                .WithOne(x => x.Project)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProjectRoleAssignment>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<ProjectRoleAssignment>()
                .HasAlternateKey(x => new { x.ProjectId, x.IdentityId, x.RoleId });


            modelBuilder.Entity<Resource>()
                .HasKey(x => x.Id);

            modelBuilder.Entity<Resource>()
                .ToTable("Resources");

            modelBuilder.Entity<Catlet>()
                .ToTable("Catlets");

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

            modelBuilder.Entity<Catlet>()
                .HasOne(x => x.Host)
                .WithMany(x => x.Catlets);

            modelBuilder.Entity<Catlet>()
                .HasMany(x => x.NetworkAdapters)
                .WithOne(x => x.Catlet)
                .HasForeignKey(x => x.CatletId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Catlet>()
                .Navigation(x => x.NetworkAdapters)
                .AutoInclude();


            modelBuilder.Entity<Catlet>()
                .HasMany(x => x.Drives)
                .WithOne(x => x.Catlet)
                .HasForeignKey(x => x.CatletId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Catlet>()
                .Navigation(x => x.Drives)
                .AutoInclude();

            modelBuilder.Entity<Catlet>()
                .Property(e => e.Features)
                .HasConversion(
                    v => string.Join(',', v),
                    v => v.Split(',',
                            StringSplitOptions.RemoveEmptyEntries)
                        .Map(Enum.Parse<CatletFeature>)
                        .ToList());


            modelBuilder.Entity<VirtualNetwork>()
                .ToTable("VNetworks")
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


            modelBuilder.Entity<FloatingNetworkPort>()
                .HasOne(x => x.AssignedPort)
                .WithOne(x => x.FloatingPort)
                .HasForeignKey<VirtualNetworkPort>(x => x.FloatingPortId)
                .OnDelete(DeleteBehavior.SetNull);


            modelBuilder.Entity<FloatingNetworkPort>()
                .Property(x => x.PoolName).HasColumnName(nameof(FloatingNetworkPort.PoolName));
            modelBuilder.Entity<FloatingNetworkPort>()
                .Property(x => x.SubnetName).HasColumnName(nameof(FloatingNetworkPort.SubnetName));

            modelBuilder.Entity<ProviderRouterPort>()
                .Property(x => x.SubnetName).HasColumnName(nameof(ProviderRouterPort.SubnetName));
            modelBuilder.Entity<ProviderRouterPort>()
                .Property(x => x.PoolName).HasColumnName(nameof(ProviderRouterPort.PoolName));


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


            modelBuilder.Entity<CatletFarm>()
                .ToTable("CatletFarms");


            modelBuilder.Entity<CatletNetworkAdapter>().HasKey(x=>new {x.CatletId, x.Id});


            modelBuilder.Entity<CatletDrive>()
                .ToTable("CatletDrives")
                .HasKey(x => x.Id);

            modelBuilder.Entity<CatletDrive>()
                .HasOne(x => x.AttachedDisk)
                .WithMany(x => x.AttachedDrives)
                .HasForeignKey(x => x.AttachedDiskId);


            modelBuilder.Entity<VirtualDisk>()
                .ToTable("CatletDisks");
                
            modelBuilder.Entity<VirtualDisk>().HasOne(x => x.Parent)
                .WithMany(x => x.Childs)
                .HasForeignKey(x => x.ParentId);

            modelBuilder.Entity<CatletMetadata>()
                .HasKey(x => x.Id);


            //this is for SQLLite only
            //TODO: add to SQLLite Model builder extension like in https://github.com/dbosoft/SAPHub/blob/main/src/SAPHub.StateDb/SqlModelBuilder.cs
            modelBuilder.Entity<OperationLogEntry>().Property(e => e.Timestamp).HasConversion(
                dateTimeOffset => dateTimeOffset.UtcDateTime,
                dateTime => new DateTimeOffset(dateTime));
        }
    }
}