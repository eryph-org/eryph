using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
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
        public DbSet<OperationResource> OperationResources { get; set; }

        public DbSet<Machine> Machines { get; set; }
        public DbSet<VirtualMachine> VirtualMachines { get; set; }
        public DbSet<VMHostMachine> VMHosts { get; set; }

        public DbSet<VirtualMachineNetworkAdapter> VirtualMachineNetworkAdapters { get; set; }
        public DbSet<VirtualMachineDrive> VirtualMachineDrives { get; set; }
        public DbSet<VirtualDisk> VirtualDisks { get; set; }

        public DbSet<Network> Networks { get; set; }
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

            modelBuilder.Entity<Network>().HasMany(c => c.Subnets)
                .WithOne(one => one.Network).HasForeignKey(fk => fk.NetworkId);

            modelBuilder.Entity<Machine>()
                .HasMany(x => x.Networks)
                .WithOne(x => x.Machine)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

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
                .HasMany(x => x.Drives)
                .WithOne(x => x.Vm)
                .HasForeignKey(x => x.MachineId)
                .OnDelete(DeleteBehavior.Cascade);

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
        }
    }

    public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
    {
    }

    public class StateStoreRepository<T> : IStateStoreRepository<T> where T : class
    {
        private readonly StateStoreContext _dbContext;
        private readonly ISpecificationEvaluator<T> _specificationEvaluator;

        public StateStoreRepository(StateStoreContext dbContext)
        {
            _dbContext = dbContext;
            _specificationEvaluator = new SpecificationEvaluator<T>();
        }

        public async Task<T> AddAsync(T entity)
        {
            await _dbContext.Set<T>().AddAsync(entity);

            return entity;
        }

        public async Task UpdateAsync(T entity)
        {
            _dbContext.Entry(entity).State = EntityState.Modified;
        }

        public async Task DeleteAsync(T entity)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        public async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().RemoveRange(entities);
        }

        public async Task SaveChangesAsync()
        {
            await _dbContext.SaveChangesAsync();
        }

        public async Task<T> GetByIdAsync(int id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<T> GetByIdAsync<TId>(TId id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<T> GetBySpecAsync(ISpecification<T> specification)
        {
            return (await ListAsync(specification)).FirstOrDefault();
        }

        public async Task<TResult> GetBySpecAsync<TResult>(ISpecification<T, TResult> specification)
        {
            return (await ListAsync(specification)).FirstOrDefault();
        }

        public async Task<List<T>> ListAsync()
        {
            return await _dbContext.Set<T>().ToListAsync();
        }

        public async Task<List<T>> ListAsync(ISpecification<T> specification)
        {
            return await ApplySpecification(specification).ToListAsync();
        }

        public async Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification)
        {
            return await ApplySpecification(specification).ToListAsync();
        }

        public async Task<int> CountAsync(ISpecification<T> specification)
        {
            return await ApplySpecification(specification).CountAsync();
        }


        private IQueryable<T> ApplySpecification(ISpecification<T> specification)
        {
            return _specificationEvaluator.GetQuery(_dbContext.Set<T>().AsQueryable().AsNoTracking(), specification);
        }

        private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> specification)
        {
            if (specification is null) throw new ArgumentNullException(nameof(specification));
            if (specification.Selector is null) throw new SelectorNotFoundException();

            return _specificationEvaluator.GetQuery(_dbContext.Set<T>().AsQueryable().AsNoTracking(), specification);
        }
    }
}