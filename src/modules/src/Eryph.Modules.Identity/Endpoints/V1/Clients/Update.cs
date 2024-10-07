using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class Update(
    IClientService clientService,
    IOpenIddictScopeManager scopeManager,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<UpdateClientRequest>
        .WithActionResult<Client>
{
    [Authorize(Policy = "identity:clients:write")]
    [HttpPut("clients/{id}")]
    [SwaggerOperation(
        Summary = "Update a client",
        Description = "Update a client",
        OperationId = "Clients_Update",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(Status200OK, "Success", typeof(Client))]
    public override async Task<ActionResult<Client>> HandleAsync(
        [FromRoute] UpdateClientRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        
        await request.Client.ValidateScopes(scopeManager, ModelState, cancellationToken);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var tenantId = userInfoProvider.GetUserTenantId();
        var persistentDescriptor = await clientService.Get(request.Id, tenantId, cancellationToken);
        if (persistentDescriptor == null)
            return NotFound();

        persistentDescriptor.Scopes.Clear();
        persistentDescriptor.Scopes.UnionWith(request.Client.AllowedScopes);
        persistentDescriptor.DisplayName = request.Client.Name;

        await clientService.Update(persistentDescriptor, cancellationToken);

        var updatedClient = persistentDescriptor.ToClient<Client>();

        return Ok(updatedClient);
    }
}
