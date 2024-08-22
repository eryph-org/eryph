using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Messages.Resources.Disks;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Dbosoft.Functional.Validations.ComplexValidations;

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
        Summary = "Creates a virtual disk",
        Description = "Creates a virtual disk",
        OperationId = "VirtualDisks_Create",
        Tags = ["Virtual Disks"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromBody] NewVirtualDiskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        var projectAccess = await userRightsProvider.HasProjectAccess(request.ProjectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");

        var diskExists = await repository.AnyAsync(
            new VirtualDiskSpecs.GetByName(
                request.ProjectId,
                DataStoreName.New(request.Store).Value,
                EnvironmentName.New(request.Environment).Value,
                StorageIdentifier.New(request.Location).Value,
                CatletDriveName.New(request.Name).Value),
            cancellationToken);
        if (diskExists)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "A disk with this name already exists.");

        return await base.HandleAsync(request, cancellationToken);
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(
        NewVirtualDiskRequest request) =>
        ValidateProperty(request, r => r.Name, CatletDriveName.NewValidation, required: true)
        | ValidateProperty(request, r => r.Location, StorageIdentifier.NewValidation, required: true)
        | ValidateProperty(request, r => r.Size, CatletConfigValidations.ValidateCatletDriveSize, required: true)
        | ValidateProperty(request, r => r.Environment, EnvironmentName.NewValidation)
        | ValidateProperty(request, r => r.Store, DataStoreName.NewValidation);
}
