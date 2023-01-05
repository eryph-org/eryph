using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Operations
{
    public class Get : GetEntityEndpoint<OperationRequest, Operation, StateDb.Model.Operation>
    {
        public Get([NotNull] IGetRequestHandler<StateDb.Model.Operation, Operation> requestHandler, 
            [NotNull] ISingleEntitySpecBuilder<OperationRequest, StateDb.Model.Operation> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("operations/{id}")]
        [SwaggerOperation(
            Summary = "Get a operation",
            Description = "Get a operation",
            OperationId = "Operations_Get",
            Tags = new[] { "Operations" })
        ]
        public override Task<ActionResult<Operation>> HandleAsync([FromRoute] OperationRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }


    }
}
