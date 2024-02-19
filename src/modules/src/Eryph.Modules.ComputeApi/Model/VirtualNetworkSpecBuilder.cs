using System;
using Ardalis.Specification;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetworkSpecBuilder : ResourceSpecBuilder<StateDb.Model.VirtualNetwork>,
    IListEntitySpecBuilder<ProjectListRequest, StateDb.Model.VirtualNetwork>
{
    private readonly IUserRightsProvider _userRightsProvider;

    public VirtualNetworkSpecBuilder(IUserRightsProvider userRightsProvider) : base(userRightsProvider)
    {
        _userRightsProvider = userRightsProvider;
    }

    public ISpecification<StateDb.Model.VirtualNetwork> GetEntitiesSpec(ProjectListRequest request)
    {
        return new ResourceSpecs<StateDb.Model.VirtualNetwork>.GetAllForProject(_userRightsProvider.GetUserTenantId(), request.Project);
    }

}