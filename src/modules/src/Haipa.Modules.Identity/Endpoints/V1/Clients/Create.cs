using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Haipa.ModuleCore;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Haipa.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]

    public class Create : BaseAsyncEndpoint
        .WithRequest<Client>
        .WithResponse<ClientWithSecrets>
    {
        private readonly IClientService<Client> _clientService;
        private readonly IEndpointResolver _endpointResolver;

        public Create(IClientService<Client> clientService, IEndpointResolver endpointResolver)
        {
            _clientService = clientService;
            _endpointResolver = endpointResolver;
        }


        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPost("clients")]
        [SwaggerOperation(
            Summary = "Creates a new client",
            Description = "Creates a client",
            OperationId = "Clients_Create",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status201Created, "Success", typeof(ClientWithSecrets))]

        public override async Task<ActionResult<ClientWithSecrets>> HandleAsync([FromBody] Client client, CancellationToken cancellationToken = new CancellationToken())
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            client.Id = Guid.NewGuid().ToString();

            var privateKey = await client.NewClientCertificate();

            await _clientService.AddClient(client);

            var createdClient = new ClientWithSecrets
            {
                Id = client.Id,
                AllowedScopes = client.AllowedScopes,
                Description = client.Description,
                Name = client.Name,
                Key = privateKey,
                KeyType = ClientSecretType.RsaPrivateKey
            };

            var clientUri = new Uri(_endpointResolver.GetEndpoint("identity") + $"/v1/clients/{client.Id}");
            return Created(clientUri,createdClient);
        }
    }


}
