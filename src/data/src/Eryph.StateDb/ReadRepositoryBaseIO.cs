using System.Threading;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.StateDb;

public class ReadRepositoryBaseIO<T> : IReadRepositoryBaseIO<T> where T : class
{
    private readonly IReadRepositoryBase<T> _innerRepository;

    public ReadRepositoryBaseIO(IReadRepositoryBase<T> innerRepository)
    {
        _innerRepository = innerRepository;
    }

    public EitherAsync<Error, Option<T>> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default) where TId : notnull
    {
        return TryAsync<Option<T>>(async () => await _innerRepository.GetByIdAsync(id, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, Option<T>> GetBySpecAsync<Spec>(Spec specification, CancellationToken cancellationToken = default) where Spec : ISingleResultSpecification, ISpecification<T>
    {
        return TryAsync<Option<T>>(async () => await _innerRepository.GetBySpecAsync(specification, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, Option<TResult>> GetBySpecAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
    {
        return TryAsync<Option<TResult>>(async () => await _innerRepository.GetBySpecAsync(specification, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, Seq<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.ListAsync(cancellationToken))
            .ToEither().Map(r => r.ToSeq());
    }

    public EitherAsync<Error, Seq<T>> ListAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.ListAsync(specification, cancellationToken))
            .ToEither().Map(r => r.ToSeq());
    }

    public EitherAsync<Error, Seq<TResult>> ListAsync<TResult>(ISpecification<T, TResult> specification, CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.ListAsync(specification, cancellationToken))
            .ToEither().Map(r => r.ToSeq());
    }

    public EitherAsync<Error, int> CountAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.CountAsync(specification, cancellationToken))
            .ToEither();
    }

    public EitherAsync<Error, int> CountAsync(CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.CountAsync(cancellationToken))
            .ToEither();
    }

public EitherAsync<Error, bool> AnyAsync(ISpecification<T> specification, CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.AnyAsync(specification,cancellationToken))
            .ToEither();
    }

public EitherAsync<Error, bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        return TryAsync(_innerRepository.AnyAsync(cancellationToken))
            .ToEither();
    }
}