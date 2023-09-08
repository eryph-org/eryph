using System.Collections.Generic;
using System.Threading;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.StateDb;

public class RepositoryBaseIO<T> :ReadRepositoryBaseIO<T>, IRepositoryBaseIO<T> where T : class
{
    private readonly IRepositoryBase<T> _innerRepository;

    public RepositoryBaseIO(IRepositoryBase<T> innerRepository) : base(innerRepository)
    {
        _innerRepository = innerRepository;
    }

    public EitherAsync<Error, T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync<T>(_innerRepository.AddAsync(entity, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await _innerRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
                return entity;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await _innerRepository.DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
                return Prelude.unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await _innerRepository.DeleteRangeAsync(entities, cancellationToken).ConfigureAwait(false);
                return Prelude.unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(_innerRepository.SaveChangesAsync(cancellationToken))
            .ToEither();
    }
}