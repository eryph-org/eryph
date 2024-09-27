using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Create(
    [NotNull] ICreateEntityRequestHandler<Catlet> operationHandler,
    IReadonlyStateStoreRepository<Catlet> repository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewCatletRequest, Catlet>(operationHandler)
{
    protected override object CreateOperationMessage(NewCatletRequest request)
    {
        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(request.Configuration);
        var config = CatletConfigDictionaryConverter.Convert(configDictionary);

        return new CreateCatletCommand
        { 
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            TenantId = userRightsProvider.GetUserTenantId(),
            Config = config,
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets")]
    [SwaggerOperation(
        Summary = "Creates a new catlet",
        Description = "Creates a catlet",
        OperationId = "Catlets_Create",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] NewCatletRequest request,
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
