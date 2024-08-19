using System;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Modules.AspNetCore;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Resources.Machines;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

using static LanguageExt.Prelude;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class Stop(
    [NotNull] IOperationRequestHandler<Catlet> operationHandler,
    [NotNull] ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<StopCatletRequest, Catlet>(operationHandler, specBuilder)
{
    protected override object CreateOperationMessage(Catlet model, StopCatletRequest request)
    {
        return new StopCatletCommand
        {
            CatletId = model.Id, 
            Mode = request.Body.Mode,
        };
    }

    [Authorize(Policy = "compute:catlets:control")]
    [HttpPut("catlets/{id}/stop")]
    [SwaggerOperation(
        Summary = "Stops a catlet",
        Description = "Stops a catlet",
        OperationId = "Catlets_Stop",
        Tags = ["Catlets"])
    ]
    public override async Task<ActionResult<ListResponse<Operation>>> HandleAsync(
        [FromRoute] StopCatletRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(request.Id, out _))
            return NotFound();

        var validation = ValidateRequest(request.Body);
        if (validation.IsFail)
            return ValidationProblem(validation.ToModelStateDictionary());

        return await base.HandleAsync(request, cancellationToken);
    }

    private static Validation<ValidationIssue, Unit> ValidateRequest(StopCatletRequestBody requestBody) =>
        ComplexValidations.ValidateProperty(requestBody, r => r.Mode, mode =>
            from _ in guard(mode is CatletStopMode.Shutdown or CatletStopMode.Hard,
                    Error.New($"The stop mode '{mode}' is not yet supported."))
                .ToValidation()
            select mode);
}
