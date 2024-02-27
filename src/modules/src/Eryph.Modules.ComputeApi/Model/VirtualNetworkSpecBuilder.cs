using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

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
        var sufficientRoles = _userRightsProvider.GetResourceRoles<StateDb.Model.VirtualNetwork>(AccessRight.Read);
        return new ResourceSpecs<StateDb.Model.VirtualNetwork>
            .GetAll(_userRightsProvider.GetAuthContext(), sufficientRoles, request.ProjectId, 
                query => query.Include(x=>x.Project));
    }

}