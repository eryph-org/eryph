using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.StateDb;

public interface IStateStore
{
    IStateStoreRepository<T> For<T>() where T : class;
    IReadonlyStateStoreRepository<T> Read<T>() where T : class;

    EitherAsync<Error, T> ReadBySpecAsync<T, Spec>(Spec specification, Error notFound, CancellationToken cancellationToken = default)
        where T: class
        where Spec : ISingleResultSpecification, ISpecification<T>;

    EitherAsync<Error, T> GetBySpecAsync<T, Spec>(Spec specification, Error notFound, CancellationToken cancellationToken = default)
        where T : class
        where Spec : ISingleResultSpecification, ISpecification<T>;


    public Task LoadPropertyAsync<T,TProperty>(T entry, Expression<Func<T, TProperty?>> propertyExpression,
        CancellationToken cancellationToken = default)
        where T : class
        where TProperty : class;

    public void LoadProperty<T, TProperty>(T entry, Expression<Func<T, TProperty?>> propertyExpression)
        where T : class
        where TProperty : class;


    public Task LoadCollectionAsync<T, TProperty>(T entry, Expression<Func<T, IEnumerable<TProperty>>> propertyExpression,
        CancellationToken cancellationToken = default)
        where T : class
        where TProperty : class;

    public void LoadCollection<T, TProperty>(T entry, Expression<Func<T, IEnumerable<TProperty>>> propertyExpression)
        where T : class
        where TProperty : class;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

}