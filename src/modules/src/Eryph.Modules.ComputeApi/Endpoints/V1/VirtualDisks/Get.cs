﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(VirtualDisk),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<VirtualDisk>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
