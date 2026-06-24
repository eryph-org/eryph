using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Get(
    IGetRequestHandler<VirtualDisk, Model.V1.VirtualDisk> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, VirtualDisk> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, Model.V1.VirtualDisk, VirtualDisk>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("virtualdisks/{id}")]
    [SwaggerOperation(
            Summary = "Get a virtual disk",
            Description = "Get a virtual disk",
            OperationId = "VirtualDisks_Get",
            Tags = ["Virtual Disks"]),
    ]
    [SwaggerResponse(
            StatusCodes.Status200OK,
            "Success",
            typeof(Model.V1.VirtualDisk), "application/json"),
    ]
    public override async Task<ActionResult<Model.V1.VirtualDisk>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
