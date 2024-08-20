using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Create(
    [NotNull] ICreateEntityRequestHandler<VirtualDisk> operationHandler,
    IReadonlyStateStoreRepository<VirtualDisk> repository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewVirtualDiskRequest, VirtualDisk>(operationHandler)
{
    protected override object CreateOperationMessage(NewVirtualDiskRequest request)
    {
        return new CreateVirtualDiskCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            TenantId = userRightsProvider.GetUserTenantId(),
            ProjectId = request.ProjectId,
            Name = request.Name,
            DataStore = request.Store,
            Environment = request.Environment,
            StorageIdentifier = request.Location,
            Size = request.Size,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("virtualdisks")]
    [SwaggerOperation(
        Summary = "Creates a new virtual disk",
        Description = "Creates a virtual disk",
        OperationId = "VirtualDisks_Create",
        Tags = ["Virtual Disks"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromBody] NewVirtualDiskRequest request,
        CancellationToken cancellationToken = default)
    {
        // TODO validation

        var projectAccess = await userRightsProvider.HasProjectAccess(request.ProjectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");


        // TODO check if the disk already exists

        return await base.HandleAsync(request, cancellationToken);
    }
}