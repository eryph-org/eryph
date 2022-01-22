using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider
{
    public interface IListResourceSpecBuilder<T>
    {

        ISpecification<T> GetResourceSpec(ListRequest request);
    }
}