using Ardalis.Specification;

namespace Haipa.StateDb
{
    public interface IStateStoreRepository<T> : IRepositoryBase<T> where T : class
    {
        T Detach(T entity);
    }

    public interface IReadonlyStateStoreRepository<T> : IReadRepositoryBase<T> where T : class
    {
    }
}