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
    public class Get : GetResourceEndpoint<VirtualMachine, StateDb.Model.VirtualMachine>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.VirtualMachine> requestHandler, 
            [NotNull] ISingleResourceSpecBuilder<StateDb.Model.VirtualMachine> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("virtualmachines/{id}")]
        [SwaggerOperation(
            Summary = "Get a Virtual Machines",
            Description = "Get a Virtual Machines",
            OperationId = "VirtualMachines_Get",
            Tags = new[] { "Virtual Machines" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualMachine))]

        public override Task<ActionResult<VirtualMachine>> HandleAsync([FromRoute] SingleResourceRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
