using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class Get(
    IClientService clientService,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<GetClientRequest>
        .WithActionResult<Client>
{
    [Authorize(Policy = "identity:clients:read")]
    [HttpGet("clients/{id}")]
    [SwaggerOperation(
        Summary = "Get a client",
        Description = "Get a client",
        OperationId = "Clients_Get",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(Status200OK, "Success", typeof(Client))]
    public override async Task<ActionResult<Client>> HandleAsync(
        [FromRoute] GetClientRequest request, 
        CancellationToken cancellationToken = default)
    {
        var tenantId = userInfoProvider.GetUserTenantId();
        var descriptor = await clientService.Get(request.Id, tenantId, cancellationToken);

        if (descriptor == null)
            return NotFound();

        var client = descriptor.ToClient<Client>();
        return Ok(client);
    }
}
