using Ardalis.Specification;

namespace Eryph.StateDb
{
    public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
    {
        T Detach(T entity);
    }
}