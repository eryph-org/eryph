using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eryph.StateDb;

public class ReadOnlyStateStoreRepository<T>(StateStoreContext dbContext) : IReadonlyStateStoreRepository<T>
    where T : class
{
    private readonly ISpecificationEvaluator _specificationEvaluator = new SpecificationEvaluator();


    public IReadRepositoryBaseIO<T> IO => new ReadRepositoryBaseIO<T>(this);

    public async Task<T?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default)
        where TId : notnull
    {
        var result = await dbContext.Set<T>().FindAsync([id], cancellationToken);
        if (result != null)
            dbContext.Entry(result).State = EntityState.Detached;
        return result;
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
        return await dbContext.Set<T>().AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<List<T>> ListAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).ToListAsync(cancellationToken);
    }

    public async Task<List<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).CountAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<T>().CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> AnyAsync(ISpecification<T> specification,
        CancellationToken cancellationToken = default)
    {
        return await ApplySpecification(specification).AnyAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public virtual async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<T>().AnyAsync(cancellationToken);
    }


    private IQueryable<T> ApplySpecification(ISpecification<T> specification)
    {
        return _specificationEvaluator.GetQuery(dbContext.Set<T>().AsQueryable().AsNoTracking(), specification);
    }

    private IQueryable<TResult> ApplySpecification<TResult>(ISpecification<T, TResult> specification)
    {
        if (specification is null) throw new ArgumentNullException(nameof(specification));
        return specification.Selector is null ? throw new SelectorNotFoundException() : _specificationEvaluator.GetQuery(dbContext.Set<T>().AsQueryable().AsNoTracking(), specification);
    }
}
