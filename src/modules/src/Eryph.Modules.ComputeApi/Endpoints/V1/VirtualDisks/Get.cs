using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Get(
    IGetRequestHandler<StateDb.Model.VirtualDisk, VirtualDisk> requestHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.VirtualDisk> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, VirtualDisk, StateDb.Model.VirtualDisk>(requestHandler, specBuilder)
{
    [Authorize(Policy = "compute:catlets:read")]
    [HttpGet("virtualdisks/{id}")]
    [SwaggerOperation(
        Summary = "Get a virtual disk",
        Description = "Get a virtual disk",
        OperationId = "VirtualDisks_Get",
        Tags = ["Virtual Disks"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualDisk))]

    public override async Task<ActionResult<VirtualDisk>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
