using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Eryph.Core;
using Eryph.StateDb.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Eryph.StateDb;

public abstract class StateStoreContext(DbContextOptions options) : DbContext(options)
{
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

    public DbSet<CatletMetadataGene> MetadataGenes { get; set; }

    public DbSet<CatletSpecification> CatletSpecifications { get; set; }

    public DbSet<CatletSpecificationVersion> CatletSpecificationVersions { get; set; }

    public DbSet<CatletSpecificationVersionGene> CatletSpecificationVersionGenes { get; set; }

    public DbSet<Project> Projects { get; set; }

    public DbSet<ProjectRoleAssignment> ProjectRoles { get; set; }

    public DbSet<Tenant> Tenants { get; set; }

    public DbSet<Gene> Genes { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // The change tracking in the controller module uses transaction
        // interceptors to detect changes. Hence, we force EF Core
        // to always create transactions.
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OperationModel>().HasMany(c => c.LogEntries);
        modelBuilder.Entity<OperationModel>().HasMany(c => c.Tasks);
        modelBuilder.Entity<OperationModel>().HasMany(c => c.Resources);
        modelBuilder.Entity<OperationModel>().HasMany(c => c.Projects);

        modelBuilder.Entity<OperationModel>()
            .Property(x => x.LastUpdated)
            .IsConcurrencyToken();

        modelBuilder.Entity<OperationTaskModel>()
            .Property(x => x.LastUpdated)
            .IsConcurrencyToken();

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

        modelBuilder.Entity<ProjectRoleAssignment>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<ProjectRoleAssignment>()
            .HasAlternateKey(x => new { x.ProjectId, x.IdentityId, x.RoleId });

        modelBuilder.Entity<Resource>()
            .UseTpcMappingStrategy()
            .HasKey(x => x.Id);

        modelBuilder.Entity<Catlet>()
            .HasMany(x => x.ReportedNetworks)
            .WithOne(x => x.Catlet)
            .HasForeignKey(x => x.CatletId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Catlet>()
            .HasOne(x => x.Host)
            .WithMany(x => x.Catlets)
            .HasForeignKey(x => x.HostId)
            .OnDelete(DeleteBehavior.Restrict);

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
            .HasSetConversion();

        modelBuilder.Entity<VirtualNetwork>()
            .HasMany(x => x.NetworkPorts)
            .WithOne(x => x.Network)
            .HasForeignKey(x => x.NetworkId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkRouterPort>()
            .HasOne(x => x.RoutedNetwork)
            .WithOne(x => x.RouterPort)
            .HasForeignKey<NetworkRouterPort>(x => x.RoutedNetworkId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<VirtualNetwork>()
            .HasMany(x => x.Subnets)
            .WithOne(x => x.Network)
            .HasForeignKey(x => x.NetworkId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<NetworkPort>()
            .HasKey(x=>x.Id);

        modelBuilder.Entity<NetworkPort>()
            .HasMany(x => x.IpAssignments)
            .WithOne(x => x.NetworkPort)
            .HasForeignKey(x => x.NetworkPortId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkPort>()
            .HasIndex(x => x.MacAddress)
            .IsUnique();

        modelBuilder.Entity<FloatingNetworkPort>()
            .HasOne(x => x.AssignedPort)
            .WithOne(x => x.FloatingPort)
            .HasForeignKey<VirtualNetworkPort>(x => x.FloatingPortId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Subnet>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<Subnet>()
            .HasMany(x => x.IpPools)
            .WithOne(x => x.Subnet)
            .HasForeignKey(x => x.SubnetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Subnet>()
            .HasMany(x => x.IpAssignments)
            .WithOne(x => x.Subnet)
            .HasForeignKey(x => x.SubnetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IpPool>()
            .HasKey(x => x.Id);
        modelBuilder.Entity<IpPool>()
            .Property(x => x.NextIp)
            .IsConcurrencyToken();

        modelBuilder.Entity<IpPool>()
            .HasMany(x => x.IpAssignments)
            .WithOne(x => x.Pool)
            .HasForeignKey(x => x.PoolId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IpAssignment>()
            .HasOne(x => x.NetworkPort)
            .WithMany(x => x.IpAssignments)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IpPoolAssignment>()
            .HasIndex(a => new { a.PoolId, a.Number })
            .IsUnique();


        modelBuilder.Entity<ReportedNetwork>()
            .HasKey(x => x.Id);


        modelBuilder.Entity<CatletNetworkAdapter>()
            .HasKey(x=>new {x.CatletId, x.Id});


        modelBuilder.Entity<CatletDrive>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<CatletDrive>()
            .HasOne(x => x.AttachedDisk)
            .WithMany(x => x.AttachedDrives)
            .HasForeignKey(x => x.AttachedDiskId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<VirtualDisk>()
            .HasOne(x => x.Parent)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<VirtualDisk>()
            .Property(x => x.UniqueGeneIndex)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        modelBuilder.Entity<VirtualDisk>()
            .HasIndex(x => x.UniqueGeneIndex);

        modelBuilder.Entity<CatletMetadata>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<CatletMetadata>()
            .HasMany<CatletNetworkPort>()
            .WithOne()
            .HasForeignKey(p => p.CatletMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CatletMetadata>()
            .HasMany(m => m.Genes)
            .WithOne()
            .HasForeignKey(g => g.MetadataId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CatletMetadata>()
            .Ignore(m => m.Metadata);

        modelBuilder.Entity<CatletMetadata>()
            .Property(m => m.MetadataJson);

        modelBuilder.Entity<CatletMetadata>()
            .Navigation(m => m.Genes)
            .AutoInclude();

        modelBuilder.Entity<CatletMetadataGene>()
            .HasKey(g => new { g.MetadataId, g.UniqueGeneIndex });

        modelBuilder.Entity<CatletMetadataGene>()
            .HasIndex(g => g.UniqueGeneIndex);

        modelBuilder.Entity<CatletMetadataGene>()
            .Property(g => g.UniqueGeneIndex)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        modelBuilder.Entity<ReportedNetwork>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<ReportedNetwork>()
            .Property(x => x.IpV4Addresses)
            .HasListConversion();

        modelBuilder.Entity<ReportedNetwork>()
            .Property(x => x.IpV6Addresses)
            .HasListConversion();

        modelBuilder.Entity<ReportedNetwork>()
            .Property(x => x.DnsServerAddresses)
            .HasListConversion();

        modelBuilder.Entity<ReportedNetwork>()
            .Property(x => x.IpV4Subnets)
            .HasListConversion();

        modelBuilder.Entity<ReportedNetwork>()
            .Property(x => x.IpV6Subnets)
            .HasListConversion();

        modelBuilder.Entity<Gene>()
            .Property(x => x.UniqueGeneIndex)
            .UsePropertyAccessMode(PropertyAccessMode.Property);

        modelBuilder.Entity<Gene>()
            .HasIndex(x => new { Combined = x.UniqueGeneIndex, x.LastSeenAgent })
            .IsUnique();

        modelBuilder.Entity<CatletSpecification>()
            .HasMany(s => s.Versions)
            .WithOne()
            .HasForeignKey(v => v.SpecificationId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        //modelBuilder.Entity<CatletSpecification>()
        //    .HasOne(s => s.Latest)
        //    .WithOne()
        //    .HasForeignKey<CatletSpecification>(s => s.LatestId)
        //    .IsRequired();

        modelBuilder.Entity<CatletSpecificationVersion>()
            .HasMany(s => s.Genes)
            .WithOne()
            .HasForeignKey(g => g.SpecificationVersionId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CatletSpecificationVersion>()
            .Navigation(s => s.Genes)
            .AutoInclude();

        modelBuilder.Entity<CatletSpecificationVersionGene>()
            .HasKey(g => new { g.SpecificationVersionId, g.UniqueGeneIndex });

        modelBuilder.Entity<CatletSpecificationVersionGene>()
            .HasIndex(g => g.UniqueGeneIndex);

        modelBuilder.Entity<CatletSpecificationVersionGene>()
            .Property(g => g.UniqueGeneIndex)
            .UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}
