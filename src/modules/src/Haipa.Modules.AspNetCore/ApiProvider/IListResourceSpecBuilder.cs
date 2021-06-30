using Ardalis.Specification;
using Haipa.Modules.AspNetCore.ApiProvider.Model;

namespace Haipa.Modules.AspNetCore.ApiProvider
{
    public interface IListResourceSpecBuilder<T>
    {

        ISpecification<T> GetResourceSpec(ListRequest request);
    }
}