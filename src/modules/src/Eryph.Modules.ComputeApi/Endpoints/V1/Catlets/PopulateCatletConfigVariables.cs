using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class PopulateCatletConfigVariables(
    ICreateEntityRequestHandler<Catlet> operationHandler,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<PopulateCatletConfigVariablesRequest, Catlet>(
        operationHandler)
{
    protected override object CreateOperationMessage(
        PopulateCatletConfigVariablesRequest request)
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Configuration);

        return new PopulateCatletConfigVariablesCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            Config = config,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets/config/populate-variables")]
    [SwaggerOperation(
        Summary = "Populate catlet config variables",
        Description = "Populates the variables in a config for a new catlet based on the parent.",
        OperationId = "Catlets_PopulateConfigVariables",
        Tags = ["Catlets"])
]
    public override async Task<ActionResult<Operation>> HandleAsync(
    [FromBody] PopulateCatletConfigVariablesRequest request,
    CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletConfig(
            request.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(NewCatletRequest.Configuration)));

        var config = validation.ToOption().ValueUnsafe();

        var tenantId = userRightsProvider.GetUserTenantId();

        var projectName = Optional(config.Project).Filter(notEmpty).Match(
        Some: n => ProjectName.New(n),
            None: () => ProjectName.New("default"));

        var environmentName = Optional(config.Environment).Filter(notEmpty).Match(
            Some: n => EnvironmentName.New(n),
            None: () => EnvironmentName.New("default"));

        var projectAccess = await userRightsProvider.HasProjectAccess(projectName.Value, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");

        return await base.HandleAsync(request, cancellationToken);
    }
}
