using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Update(
    [NotNull] IEntityOperationRequestHandler<Catlet> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<UpdateCatletRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, UpdateCatletRequest request )
    {
        var jsonString = request.Body.Configuration.GetRawText();

        var configDictionary = ConfigModelJsonSerializer.DeserializeToDictionary(jsonString);
        var config = CatletConfigDictionaryConverter.Convert(configDictionary);

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
        Summary = "Updates a catlet",
        Description = "Updates a catlet",
        OperationId = "Catlets_Update",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<ListEntitiesResponse<Operation>>> HandleAsync(
        [FromRoute] UpdateCatletRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        var validation = RequestValidations.ValidateCatletConfig(
            request.Body.Configuration,
            nameof(NewCatletRequest.Configuration));
        if (validation.IsFail)
            return ValidationProblem(
                detail:"The catlet configuration is invalid.",
                modelStateDictionary: validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }
}
