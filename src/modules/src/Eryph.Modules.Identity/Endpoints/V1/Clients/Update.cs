﻿using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Swashbuckle.AspNetCore.Annotations;

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
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(Client),
        contentTypes: ["application/json"])
    ]
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

        if (persistentDescriptor.ClientId == EryphConstants.SystemClientId)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The system client cannot be modified.");

        persistentDescriptor.Scopes.Clear();
        persistentDescriptor.Scopes.UnionWith(request.Client.AllowedScopes);
        persistentDescriptor.DisplayName = request.Client.Name;

        await clientService.Update(persistentDescriptor, cancellationToken);

        var updatedClient = persistentDescriptor.ToClient();

        return Ok(updatedClient);
    }
}
