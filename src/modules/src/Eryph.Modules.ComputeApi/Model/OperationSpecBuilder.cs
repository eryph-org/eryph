using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.Operations;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model;

public class OperationSpecBuilder(IUserRightsProvider userRightsProvider)
    : ISingleEntitySpecBuilder<OperationRequest, OperationModel>,
        IListEntitySpecBuilder<OperationsListRequest, OperationModel>
{
    public ISingleResultSpecification<OperationModel>? GetSingleEntitySpec(
        OperationRequest request,
        AccessRight accessRight)
    {
        if (!Guid.TryParse(request.Id, out var operationId))
            return null;
            
        return new OperationSpecs.GetById(
            operationId,
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read),
            request.Expand,
            request.LogTimestamp);
    }

    public ISpecification<OperationModel> GetEntitiesSpec(OperationsListRequest request)
    {
        return new OperationSpecs.GetAll(
            userRightsProvider.GetAuthContext(),
            userRightsProvider.GetProjectRoles(AccessRight.Read),
            request.Expand,
            request.LogTimestamp);
    }
}
