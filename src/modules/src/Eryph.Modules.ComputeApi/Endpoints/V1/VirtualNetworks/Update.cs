using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Networks;
using Eryph.Core;
using Eryph.Messages.Resources.Networks.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualNetworks;

public class Update(
    ICreateEntityRequestHandler<StateDb.Model.Project> operationHandler,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<UpdateProjectNetworksRequest, StateDb.Model.Project>(operationHandler)
{
    protected override object CreateOperationMessage(UpdateProjectNetworksRequest request)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            throw new ArgumentException("The project ID is invalid.", nameof(request));

        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Body.Configuration)
            ?? throw new ArgumentException("The configuration is missing", nameof(request));

        var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);

        return new CreateNetworksCommand
        { 
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            Config = config,
            TenantId = userRightsProvider.GetUserTenantId(),
            ProjectId = projectId,
        };
    }

    [Authorize(Policy = "compute:projects:write")]
    // ReSharper disable once StringLiteralTypo
    [HttpPut("projects/{project_id}/virtualnetworks/config")]
    [SwaggerOperation(
        Summary = "Update the virtual network configuration of a project",
        Description = "Update the virtual network configuration of a project",
        OperationId = "VirtualNetworks_UpdateConfig",
        Tags = ["Virtual Networks"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] UpdateProjectNetworksRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.ProjectId, out var projectId))
            return NotFound();

        var hasAccess = await userRightsProvider.HasProjectAccess(projectId, AccessRight.Admin);
        if (!hasAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have admin access to the given project.");

        var validation = RequestValidations.ValidateProjectNetworkConfig(
            request.Body.Configuration,
            nameof(UpdateProjectNetworksRequestBody.Configuration));
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The network configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }
}
