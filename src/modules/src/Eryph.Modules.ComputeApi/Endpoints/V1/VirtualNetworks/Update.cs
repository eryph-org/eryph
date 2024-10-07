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
        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Configuration)
            ?? throw new ArgumentException("The configuration is missing", nameof(request));

        var config = ProjectNetworksConfigDictionaryConverter.Convert(configDictionary);

        return new CreateNetworksCommand
        { 
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            Config = config,
            TenantId = userRightsProvider.GetUserTenantId()
        };
    }

    [Authorize(Policy = "compute:projects:write")]
    // ReSharper disable once StringLiteralTypo
    [HttpPatch("virtualnetworks")]
    [SwaggerOperation(
        Summary = "Modify the virtual network configuration of a project",
        Description = "Modify the virtual network configuration of a project",
        OperationId = "VirtualNetworks_Create",
        Tags = ["Virtual Networks"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] UpdateProjectNetworksRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateProjectNetworkConfig(
            request.Configuration,
            nameof(UpdateProjectNetworksRequest.Configuration));
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The network configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary());

        var config = validation.ToOption().ValueUnsafe();

        var projectName = Optional(config.Project).Filter(notEmpty).Match(
            Some: n => ProjectName.New(n),
            None: () => ProjectName.New("default"));

        var projectAccess = await userRightsProvider.HasProjectAccess(projectName.Value, AccessRight.Admin);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have admin access to the given project.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
