using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public class GenericResourceSpecBuilder<TResource> : ISingleEntitySpecBuilder<SingleEntityRequest, TResource>
    where TResource : Resource
{
    public ISingleResultSpecification<TResource> GetSingleEntitySpec(SingleEntityRequest request)
    {
        return new ResourceSpecs<TResource>.GetById(Guid.Parse(request.Id ?? throw new InvalidOperationException("Invalid id")));
    }
}