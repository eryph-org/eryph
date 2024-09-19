using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

[Route("v{version:apiVersion}")]
public class Create(
    IClientService clientService,
    IEndpointResolver endpointResolver,
    ICertificateGenerator certificateGenerator,
    ICertificateKeyPairGenerator certificateKeyPairGenerator,
    IOpenIddictScopeManager scopeManager,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<Client>
        .WithActionResult<ClientWithSecret>
{
    [Authorize(Policy = "identity:clients:write")]
    [HttpPost("clients")]
    [SwaggerOperation(
        Summary = "Creates a new client",
        Description = "Creates a client",
        OperationId = "Clients_Create",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(Status201Created, "Success", typeof(ClientWithSecret))]
    public override async Task<ActionResult<ClientWithSecret>> HandleAsync(
        [FromBody] Client client,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        client.Id = Guid.NewGuid().ToString();
        client.TenantId = userInfoProvider.GetUserTenantId();

        await client.ValidateScopes(scopeManager, ModelState, cancellationToken);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var descriptor = client.ToDescriptor();
        var privateKey = descriptor.NewClientCertificate(
            certificateGenerator,
            certificateKeyPairGenerator);

        descriptor = await clientService.Add(descriptor, false, cancellationToken);

        var createdClient = descriptor.ToClient<ClientWithSecret>();
        createdClient.Key = privateKey;

        var clientUri = new Uri(endpointResolver.GetEndpoint("identity") + $"/v1/clients/{client.Id}");
        return Created(clientUri, createdClient);
    }
}
