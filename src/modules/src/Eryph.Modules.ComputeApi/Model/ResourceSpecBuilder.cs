﻿using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Resource = Eryph.StateDb.Model.Resource;

namespace Eryph.Modules.ComputeApi.Model;

public class ResourceSpecBuilder<TResource>(IUserRightsProvider userRightsProvider)
    : ISingleEntitySpecBuilder<SingleEntityRequest, TResource>,
        IListEntitySpecBuilder<ListRequest, TResource>
    where TResource : Resource
{
    public ISingleResultSpecification<TResource> GetSingleEntitySpec(SingleEntityRequest request, AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var resourceId))
            throw new ArgumentException("The ID is not a GUID.", nameof(request));
        
        return new ResourceSpecs<TResource>.GetById(
            resourceId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetResourceRoles<TResource>(accessRight),
            CustomizeQuery);
    }

    public ISpecification<TResource> GetEntitiesSpec(ListRequest request)
    {
        return new ResourceSpecs<TResource>.GetAll(
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetResourceRoles<TResource>(AccessRight.Read),
            request.ProjectId,
            CustomizeQuery);
    }

    protected virtual void CustomizeQuery(ISpecificationBuilder<TResource> specification)
    {
    }
}
