using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class Get(
    [NotNull] IGetRequestHandler<StateDb.Model.VirtualNetwork, VirtualNetwork> requestHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, StateDb.Model.VirtualNetwork> specBuilder)
    : GetEntityEndpoint<SingleEntityRequest, VirtualNetwork, StateDb.Model.VirtualNetwork>(requestHandler,
        specBuilder)
{
    [Authorize(Policy = "compute:projects:read")]
    // ReSharper disable once StringLiteralTypo
    [HttpGet("vnetworks/{id}")]
    [SwaggerOperation(
        Summary = "Get a virtual network",
        Description = "Get a virtual network",
        OperationId = "VNetworks_Get",
        Tags = ["Virtual Networks"])
    ]
    [SwaggerResponse(Microsoft.AspNetCore.Http.StatusCodes.Status200OK, "Success", typeof(VirtualNetwork))]
    public override async Task<ActionResult<VirtualNetwork>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}
