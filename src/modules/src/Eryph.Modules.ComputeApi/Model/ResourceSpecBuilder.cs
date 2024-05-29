using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.ComputeApi.Model;

public class ResourceSpecBuilder<TResource> : ISingleEntitySpecBuilder<SingleEntityRequest, TResource>,
    IListEntitySpecBuilder<ListRequest, TResource> where TResource : Resource
{
    private readonly IUserRightsProvider _userRightsProvider;

    public ResourceSpecBuilder(IUserRightsProvider userRightsProvider)
    {
        _userRightsProvider = userRightsProvider;
    }

    public ISingleResultSpecification<TResource> GetSingleEntitySpec(SingleEntityRequest request, AccessRight accessRight)
    {
        return new ResourceSpecs<TResource>.GetById(request.Id,
            _userRightsProvider.GetAuthContext(),
            _userRightsProvider.GetResourceRoles<TResource>(accessRight),
            CustomizeQuery);
    }

    public ISpecification<TResource> GetEntitiesSpec(ListRequest request)
    {
        return new ResourceSpecs<TResource>
            .GetAll(
                _userRightsProvider.GetAuthContext(),
                _userRightsProvider.GetResourceRoles<TResource>(AccessRight.Read), 
                request.ProjectId, CustomizeQuery);
    }

    protected virtual void CustomizeQuery(ISpecificationBuilder<TResource> specification)
    {

    }
}