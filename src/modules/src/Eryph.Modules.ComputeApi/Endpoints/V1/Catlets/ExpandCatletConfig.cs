using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

// Compatibility shim for v0.4 clients (Eryph.ComputeClient v0.12/v0.13 `Test-Catlet -Id`).
// The original ExpandCatletConfigCommand pipeline was removed in v0.5; this endpoint
// validates the catlet exists and the caller has access, then expands the supplied
// config via ExpandNewCatletConfigCommand. Catlet-specific state is no longer factored
// in — clients should migrate to the new catlet specification endpoints.
public class ExpandCatletConfig(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<ExpandCatletConfigRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, ExpandCatletConfigRequest request)
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Body.Configuration);

        // Force the expansion into the catlet's project. The base endpoint already
        // checked the caller has write access to that project; ignoring whatever
        // project the payload contains prevents a caller from expanding configs in
        // the context of a project they do not own.
        config.Project = model.Project.Name;

        return new ExpandNewCatletConfigCommand
        {
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            Config = config,
            ShowSecrets = request.Body.ShowSecrets.GetValueOrDefault(),
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets/{id}/config/expand")]
    [SwaggerOperation(
        Summary = "Expand catlet config",
        Description = "Expand the supplied config in the context of an existing catlet. "
            + "Deprecated: existing catlet state is no longer factored into the expansion; "
            + "the supplied config is expanded as if for a new catlet. "
            + "Retained for compatibility with Eryph.ComputeClient v0.12 / v0.13 "
            + "(`Test-Catlet -Id`). New clients should use POST /catlets/config/expand.",
        OperationId = "Catlets_ExpandConfig",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] ExpandCatletConfigRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletConfig(
            request.Body.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail: "The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(ExpandCatletConfigRequestBody.Configuration)));

        return await base.HandleAsync(request, cancellationToken);
    }
}
