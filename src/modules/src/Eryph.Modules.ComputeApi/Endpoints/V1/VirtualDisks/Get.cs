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

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks
{
    public class Get : GetEntityEndpoint<SingleEntityRequest,VirtualDisk, StateDb.Model.VirtualDisk>
    {

        public Get([NotNull] IGetRequestHandler<StateDb.Model.VirtualDisk, VirtualDisk> requestHandler, 
            [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest,StateDb.Model.VirtualDisk> specBuilder) : base(requestHandler, specBuilder)
        {
        }

        [HttpGet("virtualdisks/{id}")]
        [SwaggerOperation(
            Summary = "Get a Virtual Disk",
            Description = "Get a Virtual Disk",
            OperationId = "VirtualDisks_Get",
            Tags = new[] { "Virtual Disks" })
        ]
        [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualDisk))]

        public override Task<ActionResult<VirtualDisk>> HandleAsync([FromRoute] SingleEntityRequest request, CancellationToken cancellationToken = default)
        {
            return base.HandleAsync(request, cancellationToken);
        }

    }
}
