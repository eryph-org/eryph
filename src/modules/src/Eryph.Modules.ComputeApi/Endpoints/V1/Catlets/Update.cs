using System;
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

public class Update(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<UpdateCatletRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, UpdateCatletRequest request )
    {
        var config = CatletConfigJsonSerializer.Deserialize(request.Body.Configuration);

        return new UpdateCatletCommand
        {
            CatletId = model.Id,
            CorrelationId = request.Body.CorrelationId.GetOrGenerate(),
            Config = config
        };
    }

    [Authorize(Policy = "compute:catlets:write")]
    [HttpPut("catlets/{id}")]
    [SwaggerOperation(
        Summary = "Update a catlet",
        Description = "Update a catlet",
        OperationId = "Catlets_Update",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] UpdateCatletRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = RequestValidations.ValidateCatletConfig(
            request.Body.Configuration);
        if (validation.IsFail)
            return ValidationProblem(
                detail:"The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary(
                    nameof(NewCatletRequest.Configuration)));

        return await base.HandleAsync(request, cancellationToken);
    }
}
