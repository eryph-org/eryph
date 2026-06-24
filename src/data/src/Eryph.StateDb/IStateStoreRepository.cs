using Ardalis.Specification;

namespace Eryph.StateDb;

public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
{
    IRepositoryBaseIO<T> IO { get; }
    T Detach(T entity);
}
