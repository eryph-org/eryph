using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.Identity.Models;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

using static Microsoft.AspNetCore.Http.StatusCodes;


namespace Eryph.Modules.Identity.Endpoints.V1.Clients
{
    [Route("v{version:apiVersion}")]
    public class NewKey : EndpointBaseAsync
        .WithRequest<NewClientKeyRequest>
        .WithActionResult<ClientWithSecrets>
    {
        private readonly IClientService<Client> _clientService;
        private readonly ICertificateGenerator _certificateGenerator;
        
        public NewKey(IClientService<Client> clientService, ICertificateGenerator certificateGenerator)
        {
            _clientService = clientService;
            _certificateGenerator = certificateGenerator;
        }


        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPost("clients/{id}/newkey")]
        [SwaggerOperation(
            Summary = "Updates a client key",
            Description = "Updates a client key",
            OperationId = "Clients_NewKey",
            Tags = new[] { "Clients" })
        ]
        [SwaggerResponse(Status200OK, "Success", typeof(ClientWithSecrets))]

        public override async Task<ActionResult<ClientWithSecrets>> HandleAsync([FromRoute] NewClientKeyRequest request, CancellationToken cancellationToken = new CancellationToken())
        {
            var client = await _clientService.GetClient(request.Id);
            if (client == null)
                return NotFound($"client with id {request.Id} not found.");


            var privateKey = await client.NewClientCertificate(_certificateGenerator);
            await _clientService.UpdateClient(client);

            var createdClient = new ClientWithSecrets
            {
                Id = client.Id,
                AllowedScopes = client.AllowedScopes,
                Description = client.Description,
                Name = client.Name,
                Key = privateKey,
                KeyType = ClientSecretType.RsaPrivateKey
            };

            return Ok(createdClient);
        }
    }
}
