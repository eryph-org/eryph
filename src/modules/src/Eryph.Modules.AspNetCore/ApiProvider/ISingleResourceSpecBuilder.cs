using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider
{
    public interface ISingleResourceSpecBuilder<T> where T : Resource
    {

        ISingleResultSpecification<T> GetSingleResourceSpec(SingleResourceRequest request);
    }
}