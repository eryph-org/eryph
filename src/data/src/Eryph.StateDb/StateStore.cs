using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.StateDb;

public class StateStore : IStateStore
{
    private readonly StateStoreContext _context;

    public StateStore(StateStoreContext context)
    {
        _context = context;
    }

    public IStateStoreRepository<T> For<T>() where T : class
    {
        return new StateStoreRepository<T>(_context);
    }

    public IReadonlyStateStoreRepository<T> Read<T>() where T : class
    {
        return new ReadOnlyStateStoreRepository<T>(_context);
    }

    public EitherAsync<Error, T> ReadBySpecAsync<T, Spec>(Spec specification, Error notFound, CancellationToken cancellationToken = default) where T : class where Spec : ISingleResultSpecification, ISpecification<T>
    {
        return Read<T>()
            .IO.GetBySpecAsync(specification, cancellationToken)
            .Bind(r => r.ToEitherAsync(notFound));
    }

    public EitherAsync<Error, T> GetBySpecAsync<T, Spec>(Spec specification, Error notFound, CancellationToken cancellationToken = default) where T : class where Spec : ISingleResultSpecification, ISpecification<T>
    {
        return For<T>()
            .IO.GetBySpecAsync(specification, cancellationToken)
            .Bind(r => r.ToEitherAsync(notFound));
    }

    public Task LoadPropertyAsync<T, TProperty>(T entry, Expression<Func<T, TProperty>> propertyExpression,
        CancellationToken cancellationToken = default) where T : class where TProperty : class
    {
        return _context.Entry(entry).Reference(propertyExpression).LoadAsync(cancellationToken);
    }

    public void LoadProperty<T, TProperty>(T entry, Expression<Func<T, TProperty>> propertyExpression) where T : class where TProperty : class
    { 
        _context.Entry(entry).Reference(propertyExpression).Load();
    }
}