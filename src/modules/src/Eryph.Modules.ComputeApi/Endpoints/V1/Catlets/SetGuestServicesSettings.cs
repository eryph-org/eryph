using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.GuestServices.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.AspNetCore.ApiProvider.Endpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.StateDb.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Operation = Eryph.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Updates the catlet's guest-services settings (the SSH session shell) by writing the External-pool
/// KVP values through the generic guest-services write. Setting the shell is a remote-access power.
/// </summary>
public class SetGuestServicesSettings(
    IEntityOperationRequestHandler<Catlet> operationHandler,
    ISingleEntitySpecBuilder<SingleEntityRequest, Catlet> specBuilder)
    : ResourceOperationEndpoint<SetGuestServicesSettingsRequest, Catlet>(operationHandler, specBuilder)
{
    // Each setting must fit the Hyper-V KVP value limit.
    private const int MaxShellLength = 512;

    // Authorized by the compute:catlets:remote-access scope; requires read (not write) project access.
    protected override AccessRight RequiredAccessRight => AccessRight.Read;

    protected override object CreateOperationMessage(Catlet model, SetGuestServicesSettingsRequest request)
    {
        var values = new Dictionary<string, string>();
        var removeKeys = new List<string>();
        ApplySetting(Constants.ShellKey, request.Body.Shell, values, removeKeys);
        ApplySetting(Constants.ShellArgsKey, request.Body.ShellArgs, values, removeKeys);

        return new SetGuestServicesDataCommand
        {
            CatletId = model.Id,
            OperationName = "Setting guest services shell",
            Values = values,
            RemoveKeys = removeKeys,
        };
    }

    [Authorize(Policy = "compute:catlets:remote-access")]
    [HttpPatch("catlets/{id}/guest-services/settings")]
    [SwaggerOperation(
            Summary = "Update the guest services settings of a catlet",
            Description =
                "Starts an operation that updates the catlet's guest-services settings (the SSH session shell). "
                + "A null field is left unchanged; an empty field clears the override.",
            OperationId = "Catlets_SetGuestServicesSettings",
            Tags = ["Catlets"]),
    ]
    public override async Task<ActionResult<Operation>> HandleAsync(
        [FromRoute] SetGuestServicesSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Body.Shell is null && request.Body.ShellArgs is null)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "At least one setting must be provided.");

        if (request.Body.Shell is { Length: > MaxShellLength }
            || request.Body.ShellArgs is { Length: > MaxShellLength })
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: $"A setting must not exceed {MaxShellLength} characters.");

        return await base.HandleAsync(request, cancellationToken);
    }

    // Null leaves the key unchanged, empty clears it (delete), any other value sets it.
    private static void ApplySetting(
        string key, string? value, Dictionary<string, string> values, List<string> removeKeys)
    {
        if (value is null)
            return;
        if (value.Length == 0)
            removeKeys.Add(key);
        else
            values[key] = value;
    }
}
