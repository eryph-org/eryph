using System.Collections.Generic;
using System.Threading;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.StateDb;

/// <summary>
/// <para>
/// A <see cref="IRepositoryBase{T}" /> can be used to query and save instances of <typeparamref name="T" />.
/// An <see cref="ISpecification{T}"/> (or derived) is used to encapsulate the LINQ queries against the database.
/// </para>
/// </summary>
/// <typeparam name="T">The type of entity being operated on by this repository.</typeparam>
public interface IRepositoryBaseIO<T> : IReadRepositoryBaseIO<T> where T : class
{
    /// <summary>
    /// Adds an entity in the database.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the <typeparamref name="T" />.
    /// </returns>
    EitherAsync<Error,T> AddAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// Updates an entity in the database
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    EitherAsync<Error, T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes an entity in the database
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    EitherAsync<Error, Unit> DeleteAsync(T entity, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes the given entities in the database
    /// </summary>
    /// <param name="entities">The entities to remove.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    EitherAsync<Error, Unit> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    /// <summary>
    /// Persists changes to the database.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    EitherAsync<Error, int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class RepositoryBaseIO<T> :ReadRepositoryBaseIO<T>, IRepositoryBaseIO<T> where T : class
{
    private readonly IRepositoryBase<T> _innerRepository;

    public RepositoryBaseIO(IRepositoryBase<T> innerRepository) : base(innerRepository)
    {
        _innerRepository = innerRepository;
    }

    public EitherAsync<Error, T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return TryAsync<T>(_innerRepository.AddAsync(entity, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return TryAsync(async () =>
            {
                await _innerRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
                return entity;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        return TryAsync(async () =>
            {
                await _innerRepository.DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
                return unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return TryAsync(async () =>
            {
                await _innerRepository.DeleteRangeAsync(entities, cancellationToken).ConfigureAwait(false);
                return unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.SaveChangesAsync(cancellationToken))
            .ToEither();
    }
}