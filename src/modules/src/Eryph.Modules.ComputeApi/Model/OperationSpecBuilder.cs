using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.ComputeApi.Endpoints.V1.Operations;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.ComputeApi.Model
{
    public class OperationSpecBuilder : ISingleEntitySpecBuilder<OperationRequest, OperationModel>, IListEntitySpecBuilder<OperationsListRequest, OperationModel>
    {
        readonly IUserRightsProvider _userRightsProvider;

        public OperationSpecBuilder(IUserRightsProvider userRightsProvider)
        {
            _userRightsProvider = userRightsProvider;
        }

        public ISingleResultSpecification<OperationModel> GetSingleEntitySpec(OperationRequest request, AccessRight accessRight)
        {
            var tenantId = _userRightsProvider.GetUserTenantId();

            return !Guid.TryParse(request.Id, out var requestId) 
                ? null 
                : new OperationSpecs.GetById(requestId, tenantId, request.Expand, request.LogTimestamp);
        }

        public ISpecification<OperationModel> GetEntitiesSpec(OperationsListRequest request)
        {
            var tenantId = _userRightsProvider.GetUserTenantId();
            var roles = _userRightsProvider.GetUserRoles();

            return new OperationSpecs.GetAll(tenantId, roles, AccessRight.Read, request.Expand, request.LogTimestamp);

        }
    }
}