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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualMachines
{
    public class Get : GetResourceEndpoint<VirtualCatlet, StateDb.Model.VirtualCatlet>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.VirtualCatlet> requestHandler, 
            [NotNull] ISingleResourceSpecBuilder<StateDb.Model.VirtualCatlet> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("virtualmachines/{id}")]
        [SwaggerOperation(
            Summary = "Get a Virtual Machines",
            Description = "Get a Virtual Machines",
            OperationId = "VirtualMachines_Get",
            Tags = new[] { "Virtual Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualCatlet))]

        public override Task<ActionResult<VirtualCatlet>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
