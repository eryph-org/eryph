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
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Dbosoft.Functional.Validations.ComplexValidations;
using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class Create(
    ICreateEntityRequestHandler<VirtualDisk> operationHandler,
    IReadonlyStateStoreRepository<VirtualDisk> repository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewVirtualDiskRequest, VirtualDisk>(operationHandler)
{
    protected override object CreateOperationMessage(NewVirtualDiskRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            throw new ArgumentException("The project ID is invalid.", nameof(request));

        return new CreateVirtualDiskCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            ProjectId = projectId,
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
        Summary = "Create a virtual disk",
        Description = "Create a virtual disk",
        OperationId = "VirtualDisks_Create",
        Tags = ["Virtual Disks"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] NewVirtualDiskRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateRequest(request)
            .ToJsonPath(ApiJsonSerializerOptions.Options.PropertyNamingPolicy);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        var projectId = Guid.Parse(request.ProjectId);

        var projectAccess = await userRightsProvider.HasProjectAccess(projectId, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");

        var diskExists = await repository.AnyAsync(
            new VirtualDiskSpecs.GetByName(
                projectId,
                Optional(request.Store).Filter(notEmpty)
                    .Map(ds => DataStoreName.New(ds))
                    .IfNone(DataStoreName.New(EryphConstants.DefaultDataStoreName))
                    .Value,
                Optional(request.Environment).Filter(notEmpty)
                    .Map(e => EnvironmentName.New(e))
                    .IfNone(EnvironmentName.New(EryphConstants.DefaultEnvironmentName))
                    .Value,
                StorageIdentifier.New(request.Location).Value,
                CatletDriveName.New(request.Name).Value),
            cancellationToken);
        if (diskExists)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A disk with the name '{request.Name}' already exists in the specified storage location. Disk names must be unique within a storage location.");

        return await base.HandleAsync(request, cancellationToken);
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(
        NewVirtualDiskRequest request) =>
        ValidateProperty(request, r => r.Name, CatletDriveName.NewValidation, required: true)
        | ValidateProperty(request, r => r.ProjectId,
            i => parseGuid(i).ToValidation(Error.New("The project ID is invalid.")), required: true)
        | ValidateProperty(request, r => r.Location, StorageIdentifier.NewValidation, required: true)
        | ValidateProperty(request, r => r.Size, CatletConfigValidations.ValidateCatletDriveSize, required: true)
        | ValidateProperty(request, r => r.Environment, EnvironmentName.NewValidation)
        | ValidateProperty(request, r => r.Store, DataStoreName.NewValidation);
}
