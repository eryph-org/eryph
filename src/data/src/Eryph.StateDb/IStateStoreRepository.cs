using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;

namespace Eryph.StateDb
{
    public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
    {
        T Detach(T entity);

        IRepositoryBaseIO<T> IO { get; }
    }
}