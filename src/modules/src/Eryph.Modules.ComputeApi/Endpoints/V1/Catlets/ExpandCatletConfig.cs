using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Threading;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ExpandCatletConfig(
    ICreateEntityRequestHandler<Catlet> operationHandler,
    IReadonlyStateStoreRepository<Catlet> repository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<ExpandCatletConfigRequest, Catlet>(
        operationHandler)
{
    protected override object CreateOperationMessage(
        ExpandCatletConfigRequest request)
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Configuration);

        return new ExpandNewCatletConfigCommand
        {
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            TenantId = userRightsProvider.GetUserTenantId(),
            Config = config,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets/config/expand")]
    [SwaggerOperation(
        Summary = "Expand new catlet config",
        Description = "Expand the config for a new catlet",
        OperationId = "Catlets_ExpandConfig",
        Tags = ["Catlets"])
]
    public override async Task<ActionResult<Operation>> HandleAsync(
    [FromBody] ExpandCatletConfigRequest request,
    CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletConfig(
            request.Configuration,
            nameof(NewCatletRequest.Configuration));
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary());

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

        var existingCatlet = await repository.GetBySpecAsync(
            new CatletSpecs.GetByName(config.Name ?? "catlet", tenantId, projectName.Value, environmentName.Value),
            cancellationToken);

        if (existingCatlet != null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "A catlet with this name already exists");

        return await base.HandleAsync(request, cancellationToken);
    }
}
