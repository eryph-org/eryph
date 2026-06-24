using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.StateDb;

public class StateStore(StateStoreContext context) : IStateStore
{
    public IStateStoreRepository<T> For<T>() where T : class
    {
        return new StateStoreRepository<T>(context);
    }

    public IReadonlyStateStoreRepository<T> Read<T>() where T : class
    {
        return new ReadOnlyStateStoreRepository<T>(context);
    }

    public EitherAsync<Error, T> ReadBySpecAsync<T, Spec>(Spec specification, Error notFound,
        CancellationToken cancellationToken = default) where T : class
        where Spec : ISingleResultSpecification, ISpecification<T>
    {
        return Read<T>()
            .IO.GetBySpecAsync(specification, cancellationToken)
            .Bind(r => r.ToEitherAsync(notFound));
    }

    public EitherAsync<Error, T> GetBySpecAsync<T, Spec>(Spec specification, Error notFound,
        CancellationToken cancellationToken = default) where T : class
        where Spec : ISingleResultSpecification, ISpecification<T>
    {
        return For<T>()
            .IO.GetBySpecAsync(specification, cancellationToken)
            .Bind(r => r.ToEitherAsync(notFound));
    }

    public Task LoadPropertyAsync<T, TProperty>(T entry, Expression<Func<T, TProperty?>> propertyExpression,
        CancellationToken cancellationToken = default) where T : class where TProperty : class
    {
        return context.Entry(entry).Reference(propertyExpression).LoadAsync(cancellationToken);
    }

    public void LoadProperty<T, TProperty>(T entry, Expression<Func<T, TProperty?>> propertyExpression)
        where T : class where TProperty : class
    {
        context.Entry(entry).Reference(propertyExpression).Load();
    }

    public Task LoadCollectionAsync<T, TProperty>(T entry,
        Expression<Func<T, IEnumerable<TProperty>>> propertyExpression,
        CancellationToken cancellationToken = default) where T : class where TProperty : class
    {
        return context.Entry(entry).Collection(propertyExpression).LoadAsync(cancellationToken);
    }

    public void LoadCollection<T, TProperty>(T entry, Expression<Func<T, IEnumerable<TProperty>>> propertyExpression)
        where T : class where TProperty : class
    {
        context.Entry(entry).Collection(propertyExpression).Load();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
