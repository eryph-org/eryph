using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Microsoft.Extensions.DependencyInjection;

namespace Eryph.StateDb
{
    public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
    {
        T Detach(T entity);

        public Task<TProperty> GetBySpecAsync<TProperty, TSpec>(T entry,
            Expression<Func<T, IEnumerable<TProperty>>> propertyExpression,
            TSpec specification,
            CancellationToken cancellationToken)
            where TProperty : class
            where TSpec : ISingleResultSpecification, ISpecification<TProperty>;

        IRepositoryBaseIO<T> IO { get; }
    }
}