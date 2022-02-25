using Ardalis.Specification;

namespace Eryph.StateDb;

public interface IReadonlyStateStoreRepository<T> : IReadRepositoryBase<T> where T : class
{
}