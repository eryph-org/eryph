using System.Collections.Generic;
using System.Threading;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.StateDb;

public class RepositoryBaseIO<T>(IRepositoryBase<T> innerRepository)
    : ReadRepositoryBaseIO<T>(innerRepository), IRepositoryBaseIO<T>
    where T : class
{
    public EitherAsync<Error, T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(innerRepository.AddAsync(entity, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await innerRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
                return entity;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await innerRepository.DeleteAsync(entity, cancellationToken).ConfigureAwait(false);
                return Prelude.unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, Unit> DeleteRangeAsync(IEnumerable<T> entities,
        CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(async () =>
            {
                await innerRepository.DeleteRangeAsync(entities, cancellationToken).ConfigureAwait(false);
                return Prelude.unit;
            })
            .ToEither();
    }

    public EitherAsync<Error, int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Prelude.TryAsync(innerRepository.SaveChangesAsync(cancellationToken))
            .ToEither();
    }
}
