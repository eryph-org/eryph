using System;
using System.Linq;
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
    ICertificateKeyService certificateKeyService,
    IOpenIddictScopeManager scopeManager,
    IUserInfoProvider userInfoProvider)
    : EndpointBaseAsync
        .WithRequest<NewClientRequestBody>
        .WithActionResult<ClientWithSecret>
{
    [Authorize(Policy = "identity:clients:write")]
    [HttpPost("clients")]
    [SwaggerOperation(
        Summary = "Create a new client",
        Description = "Create a client",
        OperationId = "Clients_Create",
        Tags = ["Clients"])
    ]
    [SwaggerResponse(
        statusCode: Status201Created,
        description: "Success",
        type: typeof(ClientWithSecret),
        contentTypes: ["application/json"])
    ]
    public override async Task<ActionResult<ClientWithSecret>> HandleAsync(
        [FromBody] NewClientRequestBody client,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        await client.ValidateScopes(scopeManager, ModelState, cancellationToken);
        client.ValidateRoles(ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var descriptor = client.ToDescriptor(userInfoProvider.GetUserTenantId());
        var privateKey = descriptor.NewClientCertificate(
            certificateGenerator,
            certificateKeyService);

        descriptor = await clientService.Add(descriptor, false, cancellationToken);

        var createdClient = descriptor.ToClient(privateKey);
        var clientUri = new Uri(endpointResolver.GetEndpoint("identity") + $"/v1/clients/{createdClient.Id}");

        return Created(clientUri, createdClient);
    }
}
