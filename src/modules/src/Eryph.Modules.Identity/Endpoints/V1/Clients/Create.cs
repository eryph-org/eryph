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


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]

    public class Create : EndpointBaseAsync
        .WithRequest<Client>
        .WithActionResult<ClientWithSecret>
    {
        private readonly IClientService _clientService;
        private readonly IEndpointResolver _endpointResolver;
        private readonly ICertificateGenerator _certificateGenerator;
        private readonly IOpenIddictScopeManager _scopeManager;
        private readonly IUserInfoProvider _userInfoProvider;

        public Create(IClientService clientService, IEndpointResolver endpointResolver, ICertificateGenerator certificateGenerator, IOpenIddictScopeManager scopeManager, IUserInfoProvider userInfoProvider)
        {
            _clientService = clientService;
            _endpointResolver = endpointResolver;
            _certificateGenerator = certificateGenerator;
            _scopeManager = scopeManager;
            _userInfoProvider = userInfoProvider;
        }


        [Authorize(Policy = "identity:clients:write")]
        [HttpPost("clients")]
        [SwaggerOperation(
            Summary = "Creates a new client",
            Description = "Creates a client",
            OperationId = "Clients_Create",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status201Created, "Success", typeof(ClientWithSecret))]

        public override async Task<ActionResult<ClientWithSecret>> HandleAsync([FromBody] Client client, CancellationToken cancellationToken = new())
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            client.Id = Guid.NewGuid().ToString();
            client.TenantId = _userInfoProvider.GetUserTenantId();

            var (scopesValid, invalidScope) = await client.ValidateScopes(_scopeManager, cancellationToken);
            if (!scopesValid)
                return BadRequest($"Invalid scope {invalidScope}.");

            var descriptor = client.ToDescriptor();
            var privateKey = await descriptor.NewClientCertificate(_certificateGenerator);

            descriptor = await _clientService.Add(descriptor, false, cancellationToken);

            var createdClient = descriptor.ToClient<ClientWithSecret>();
            createdClient.Key = privateKey;

            var clientUri = new Uri(_endpointResolver.GetEndpoint("identity") + $"/v1/clients/{client.Id}");
            return Created(clientUri,createdClient);
        }
    }


}
