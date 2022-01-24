using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Machines
{
    public class Get : GetResourceEndpoint<Machine, StateDb.Model.Machine>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.Machine> requestHandler, [NotNull] ISingleResourceSpecBuilder<StateDb.Model.Machine> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("machines/{id}")]
        [SwaggerOperation(
            Summary = "Get a Machines",
            Description = "Get a Machines",
            OperationId = "Machines_Get",
            Tags = new[] { "Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(Machine))]

        public override Task<ActionResult<Machine>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
