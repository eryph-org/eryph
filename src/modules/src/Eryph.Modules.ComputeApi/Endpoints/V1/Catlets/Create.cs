using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Yaml;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt.UnsafeValueAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Create(
    ICreateEntityRequestHandler<Catlet> operationHandler,
    IReadonlyStateStoreRepository<Catlet> repository,
    IUserRightsProvider userRightsProvider)
    : NewOperationRequestEndpoint<NewCatletRequest, Catlet>(operationHandler)
{
    protected override object CreateOperationMessage(NewCatletRequest request)
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Configuration);
        
        return new CreateCatletCommand
        { 
            CorrelationId = request.CorrelationId.GetOrGenerate(),
            TenantId = userRightsProvider.GetUserTenantId(),
            Name = string.IsNullOrWhiteSpace(config.Name) ? EryphConstants.DefaultCatletName : config.Name,
            // Convert the configuration to YAML as it is more useful to the user
            ContentType = "application/yaml",
            ConfigYaml = CatletConfigYamlSerializer.Serialize(config),
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets")]
    [SwaggerOperation(
        Summary = "Create a new catlet",
        Description = "Create a catlet",
        OperationId = "Catlets_Create",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromBody] NewCatletRequest request,
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

        var projectAccess = await userRightsProvider.HasProjectAccess(projectName.Value, AccessRight.Write);
        if (!projectAccess)
            return Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "You do not have write access to the given project.");

        var catletName = string.IsNullOrWhiteSpace(config.Name)
            ? EryphConstants.DefaultCatletName
            : config.Name;
        var existingCatlet = await repository.GetBySpecAsync(
            new CatletSpecs.GetByName(catletName, tenantId, projectName.Value),
            cancellationToken);

        if (existingCatlet != null)
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: $"A catlet with the name '{catletName}' already exists in the project '{projectName.Value}'. Catlet names must be unique within a project.");
            
        return await base.HandleAsync(request, cancellationToken);
    }
}
