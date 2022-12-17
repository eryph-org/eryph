using System;
using Ardalis.Specification;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.CommonApi.Endpoints.V1.Operations;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.CommonApi.Models
{
    public class OperationSpecBuilder : ISingleEntitySpecBuilder<OperationRequest, Operation>, IListEntitySpecBuilder<OperationsListRequest,Operation>
    {
        public ISingleResultSpecification<Operation> GetSingleEntitySpec(OperationRequest request)
        {
            return new OperationSpecs.GetById(Guid.Parse(request.Id), request.Expand, request.LogTimestamp);
        }

        public ISpecification<Operation> GetEntitiesSpec(OperationsListRequest request)
        {
            return new OperationSpecs.GetAll(request.Expand, request.LogTimestamp);

        }
    }
}