using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Delete(
    [NotNull] IOperationRequestHandler<VirtualDisk> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, VirtualDisk> specBuilder)
    : ResourceOperationEndpoint<SingleEntityRequest, VirtualDisk>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(VirtualDisk model, SingleEntityRequest request)
    {
        return new DestroyVirtualDiskCommand
        {
            DiskId = model.Id
        };
    }

    [HttpDelete("virtualdisks/{id}")]
    [SwaggerOperation(
        Summary = "Deletes a virtual disk",
        Description = "Deletes a virtual disk",
        OperationId = "VirtualDisks_Delete",
        Tags = ["Virtual Disks"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] SingleEntityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        return await base.HandleAsync(request, cancellationToken);
    }
}