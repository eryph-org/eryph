using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class Delete(
    IClientService clientService,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<DeleteClientRequest>
        .WithoutResult
{
    [Authorize(Policy = "identity:clients:write")]
    [HttpDelete("clients/{id}")]
    [SwaggerOperation(
        Summary = "Delete a client",
        Description = "Delete a client",
        OperationId = "Clients_Delete",
        Tags = ["Clients"])
    ]
    [ProducesResponseType(Status204NoContent)]
    public override async Task<ActionResult> HandleAsync(
        [FromRoute] DeleteClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = userInfoProvider.GetUserTenantId();

        var client = await clientService.Get(request.Id, tenantId, cancellationToken);
        if (client == null)
            return NotFound();

        if (client.ClientId == EryphConstants.SystemClientId)
            return Problem(
                statusCode: Status400BadRequest,
                detail: "The system client cannot be deleted.");

        await clientService.Delete(client.ClientId, tenantId, cancellationToken);

        return NoContent();
    }
}
