﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Delete(
    IEntityOperationRequestHandler<VirtualDisk> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, VirtualDisk> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, VirtualDisk>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(VirtualDisk model, SingleEntityRequest request)
    {
        return new DestroyVirtualDiskCommand
        {
            DiskId = model.Id
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpDelete("virtualdisks/{id}")]
    [SwaggerOperation(
        Summary = "Delete a virtual disk",
        Description = "Delete a virtual disk",
        OperationId = "VirtualDisks_Delete",
        Tags = ["Virtual Disks"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        return await base.HandleAsync(request, cancellationToken);
    }
}
