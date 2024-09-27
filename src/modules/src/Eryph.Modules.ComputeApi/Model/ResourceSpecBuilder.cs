using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.ComputeApi.Model;

public abstract class ResourceSpecBuilder<TResource>(IUserRightsProvider userRightsProvider)
    : ISingleEntitySpecBuilder<SingleEntityRequest, TResource>,
        IListEntitySpecBuilder<ListFilteredByProjectRequest, TResource>
    where TResource : Resource
{
    public ISingleResultSpecification<TResource>? GetSingleEntitySpec(
        SingleEntityRequest request,
        AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var resourceId))
            return null;
        
        return new ResourceSpecs<TResource>.GetById(
            resourceId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetResourceRoles<TResource>(accessRight),
            CustomizeQuery);
    }

    public ISpecification<TResource> GetEntitiesSpec(ListFilteredByProjectRequest request)
    {
        Guid? projectId = null;
        if (request.ProjectId is not null)
        { 
            if (!Guid.TryParse(request.ProjectId, out var pId))
                throw new ArgumentException("The Project ID is not a GUID.", nameof(request));

            projectId = pId;
        } 
        
        return new ResourceSpecs<TResource>.GetAll(
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetResourceRoles<TResource>(AccessRight.Read),
            projectId,
            CustomizeQuery);
    }

    protected virtual void CustomizeQuery(ISpecificationBuilder<TResource> query)
    {
    }
}
