using System.Security.Cryptography;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Core;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class NewKey(
    IClientService clientService,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyService certificateKeyService,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<NewClientKeyRequest>
        .WithActionResult<ClientWithSecret>
{
    [Authorize(Policy = "identity:clients:write")]
    [HttpPost("clients/{id}/key")]
    [SwaggerOperation(
        Summary = "Create or replace the client key",
        Description = "Create or replace the client key",
        OperationId = "Clients_NewKey",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(
        statusCode: StatusCodes.Status200OK,
        description: "Success",
        type: typeof(ClientWithSecret),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<ClientWithSecret>> HandleAsync(
        [FromRoute] NewClientKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = userInfoProvider.GetUserTenantId();
        var descriptor = await clientService.Get(request.Id, tenantId, cancellationToken);
        if (descriptor is null)
            return NotFound();

        if (descriptor.ClientId == EryphConstants.SystemClientId)
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The system client cannot be modified.");

        var sharedSecret = (request.Body.SharedSecret).GetValueOrDefault(false);
        string key;
        if (sharedSecret)
        {
            key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace('=', '0');
            descriptor.ClientSecret = key;
            descriptor = await clientService.Update(descriptor, cancellationToken);
                
        }
        else
        {
            key = descriptor.NewClientCertificate(
                certificateGenerator,
                certificateKeyService);
            descriptor = await clientService.Update(descriptor, cancellationToken);
        }

        var updatedClient = descriptor.ToClient(key);

        return Ok(updatedClient);
    }
}
