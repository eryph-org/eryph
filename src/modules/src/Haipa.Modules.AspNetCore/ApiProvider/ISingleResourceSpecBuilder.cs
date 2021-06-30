using Haipa.Data;
using Haipa.Modules.AspNetCore.ApiProvider.Model;
using Haipa.StateDb.Model;

namespace Haipa.Modules.AspNetCore.ApiProvider
{
    public interface ISingleResourceSpecBuilder<T> where T : Resource
    {

        ISingleResultSpecification<T> GetSingleResourceSpec(SingleResourceRequest request);
    }
}