using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ExpandCatletConfig(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<ExpandCatletConfigRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, ExpandCatletConfigRequest request)
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Body.Configuration);

        return new ExpandCatletConfigCommand
        {
            CatletId = model.Id,
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            Config = config
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPost("catlets/{id}/config/expand")]
    [SwaggerOperation(
        Summary = "Expand catlet config",
        Description = "Expand the config for an existing catlet",
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
                    nameof(NewCatletRequest.Configuration)));

        return await base.HandleAsync(request, cancellationToken);
    }
}
