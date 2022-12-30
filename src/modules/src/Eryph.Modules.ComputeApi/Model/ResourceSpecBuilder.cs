using System;
using System.Linq;
using System.Security.Claims;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;
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
        var tenantId = _userRightsProvider.GetUserTenantId();
        var roles = _userRightsProvider.GetUserRoles();

        return !Guid.TryParse(request.Id, out var resourceId) 
            ? null : new ResourceSpecs<TResource>.GetById(resourceId,tenantId, roles, accessRight, CustomizeQuery);
    }

    public ISpecification<TResource> GetEntitiesSpec(ListRequest request)
    {
        var tenantId = _userRightsProvider.GetUserTenantId();
        var roles = _userRightsProvider.GetUserRoles();


        return new ResourceSpecs<TResource>
            .GetAll(tenantId, roles, AccessRight.Read, request.Project, CustomizeQuery);
    }

    protected virtual void CustomizeQuery(ISpecificationBuilder<TResource> specification)
    {

    }
}