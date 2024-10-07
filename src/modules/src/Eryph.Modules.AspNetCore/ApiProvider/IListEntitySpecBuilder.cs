using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public interface IListEntitySpecBuilder<T>
{
    ISpecification<T> GetEntitiesSpec();
}

public interface IListEntitySpecBuilder<in TRequest,T>
    where TRequest : IListRequest
{
    ISpecification<T> GetEntitiesSpec(TRequest request);
}
