using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
#pragma warning disable 1998

namespace Eryph.StateDb
{

    public class StateStoreRepository<T> : IStateStoreRepository<T> where T : class
    {
        private readonly StateStoreContext _dbContext;
        private readonly ISpecificationEvaluator _specificationEvaluator;

        public StateStoreRepository(StateStoreContext dbContext)
        {
            _dbContext = dbContext;
            _specificationEvaluator = new SpecificationEvaluator();
        }

        public IRepositoryBaseIO<T> IO => new RepositoryBaseIO<T>(this);

        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbContext.Set<T>().AddAsync(entity, cancellationToken);
            return entity;
        }

        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            var entityEntry = _dbContext.Entry(entity);

            if(entityEntry.State == EntityState.Detached || entityEntry.State == EntityState.Unchanged)
                entityEntry.State = EntityState.Modified;
        }

        public async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            _dbContext.Set<T>().Remove(entity);
        }

        public async Task DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
        {
            _dbContext.Set<T>().RemoveRange(entities);
        }

        Task<int> IRepositoryBase<T>.SaveChangesAsync(CancellationToken cancellationToken)
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
            where TId : notnull
        {
            return await _dbContext.Set<T>().FindAsync(new object[] { id }, cancellationToken);
        }

        public async Task<T?> GetBySpecAsync<TSpec>(TSpec specification, 
            CancellationToken cancellationToken = default) where TSpec : ISingleResultSpecification, ISpecification<T>
        {
            return (await ListAsync(specification, cancellationToken)).FirstOrDefault();
        }

        public async Task<TResult?> GetBySpecAsync<TResult>(ISpecification<T, TResult> specification,
            CancellationToken cancellationToken = default)
        {
            return (await ListAsync(specification, cancellationToken)).FirstOrDefault();
        }


        public async Task<List<T>> ListAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<T>().ToListAsync(cancellationToken: cancellationToken);
        }

        public async Task<List<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            return await ApplySpecification(specification).ToListAsync(cancellationToken: cancellationToken);
        }

        public async Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
        {
            return await ApplySpecification(specification).ToListAsync(cancellationToken: cancellationToken);
        }

        public Task<List<TProperty>> ListAsync<TProperty>(T entry,
            Expression<Func<T, IEnumerable<TProperty>>> propertyExpression,
            ISpecification<TProperty> specification,
            CancellationToken cancellationToken)
            where TProperty : class
        {
            return _specificationEvaluator.GetQuery(_dbContext.Entry(entry)
                .Collection(propertyExpression)
                .Query(), specification).ToListAsync(cancellationToken);

        }


        public async Task<int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            return await ApplySpecification(specification).CountAsync(cancellationToken: cancellationToken);
        }


        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<T>().CountAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public virtual async Task<bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
        {
            return await ApplySpecification(specification).AnyAsync(cancellationToken);
        }

        public Task<bool> AnyAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return  _dbContext.Set<T>().AnyAsync(cancellationToken);
        }


        private IQueryable<T> ApplySpecification(ISpecification<T> specification)
        {
            return _specificationEvaluator.GetQuery(_dbContext.Set<T>().AsQueryable(), specification);
        }

        private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> specification)
        {
            if (specification is null) throw new ArgumentNullException(nameof(specification));
            if (specification.Selector is null) throw new SelectorNotFoundException();

            return _specificationEvaluator.GetQuery(_dbContext.Set<T>().AsQueryable(), specification);
        }

        public T Detach(T entity)
        {
            _dbContext.Entry(entity).State = EntityState.Detached;
            return entity;
        }
    }
}