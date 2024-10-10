using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public interface ISingleEntitySpecBuilder<in TRequest,T>
    where T : class
    where TRequest : SingleEntityRequest
{
    /// <summary>
    /// Returns the <see cref="ISingleResultSpecification{T}"/> which will load
    /// the entity which is requested by the given <paramref name="request"/>.
    /// Can return <see langword="null"/> when it is clear that no result can be found.
    /// </summary>
    ISingleResultSpecification<T>? GetSingleEntitySpec(TRequest request, AccessRight accessRight);
}
