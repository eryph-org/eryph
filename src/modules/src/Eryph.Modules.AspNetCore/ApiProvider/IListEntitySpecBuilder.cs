using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public interface IListEntitySpecBuilder<in TRequest,T> where TRequest : IListEntitiesRequest
{
    ISpecification<T> GetEntitiesSpec(TRequest request);
}
