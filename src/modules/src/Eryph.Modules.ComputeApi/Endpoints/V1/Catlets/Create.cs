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
using Microsoft.AspNetCore.Authorization;
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
        var jsonString = request.Configuration.GetValueOrDefault().ToString();

        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
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
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromBody] NewCatletRequest request,
        CancellationToken cancellationToken = default)
    {
        var jsonString = request.Configuration.GetValueOrDefault().ToString();

        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
        var config = CatletConfigDictionaryConverter.Convert(configDictionary);

        var validation = CatletConfigValidations.ValidateCatletConfig(
            config, nameof(NewCatletRequest.Configuration));
        if (validation.IsFail)
            return BadRequest(validation.ToModelStateDictionary());

        var tenantId = userRightsProvider.GetUserTenantId();
            
        var projectName = Optional(config.Project).Filter(notEmpty).Match(
            Some: n => ProjectName.New(n),
            None: () => ProjectName.New("default"));

        var environmentName = Optional(config.Environment).Filter(notEmpty).Match(
            Some: n => EnvironmentName.New(n),
            None: () => EnvironmentName.New("default"));

        var projectAccess = await userRightsProvider.HasProjectAccess(projectName.Value, AccessRight.Write);
        if (!projectAccess)
            return Forbid();

        var existingCatlet = await repository.GetBySpecAsync(
            new CatletSpecs.GetByName(config.Name ?? "catlet", tenantId, projectName.Value, environmentName.Value),
            cancellationToken);

        if (existingCatlet != null)
            return Conflict();
            
        return await base.HandleAsync(request, cancellationToken);
    }
}
