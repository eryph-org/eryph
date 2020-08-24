using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.ApiProvider;
using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Haipa.Modules.Identity.Controllers.V1
{
    using static Microsoft.AspNetCore.Http.StatusCodes;

    /// <inheritdoc />
    /// <summary>
    /// Defines the <see cref="!:ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [Authorize(Policy = "identity:clients:read:all")]
    [ApiExceptionFilter]
    public class ClientsController : ApiController
    {
        private readonly IClientService<Client> _clientService;

        public ClientsController(IClientService<Client> clientService)
        {
            _clientService = clientService;
        }


        /// <summary>
        /// Queries for Clients.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [SwaggerOperation(OperationId = "Clients_List")]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValue<IEnumerable<Client>>))]
        [EnableQuery]
        
        public IQueryable<Client> Get()
        {
            return _clientService.QueryClients();
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Clients_Get")]
        [SwaggerResponse(Status200OK, "Success", typeof(Client))]
        public async Task<IActionResult> Get([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);

            if (client == null) 
                return NotFound($"client with id {key} not found.");

            return Ok(client);
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpDelete]
        [SwaggerOperation(OperationId = "Clients_Delete")]
        [ProducesResponseType(Status200OK)]
        public async Task<IActionResult> Delete([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);
            if (client == null)
                return NotFound($"client with id {key} not found.");

            await _clientService.DeleteClient(client);

            return Ok();
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPut]
        [SwaggerOperation(OperationId = "Clients_Update")]
        [SwaggerResponse(Status200OK, "Success", typeof(Client))]
        public Task<IActionResult> Put([FromODataUri] string key, [FromBody] Delta<Client> client)
        {
            return PutOrPatch(key, client, true);
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPatch]
        [SwaggerOperation(OperationId = "Clients_Change")]
        [SwaggerResponse(Status200OK, "Success", typeof(Client))]
        public Task<IActionResult> Patch([FromODataUri] string key, [FromBody] Delta<Client> client)
        {
            return PutOrPatch(key, client, false);
        }


        private async Task<IActionResult> PutOrPatch([FromODataUri] string clientId, Delta<Client> client,
            bool putMode)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var persistentClient = await _clientService.GetClient(clientId);
            if (persistentClient == null)
                return NotFound($"client with id {clientId} not found.");

            if (putMode)
                client.Put(persistentClient);
            else
                client.Patch(persistentClient);

            persistentClient.Id = clientId;
            await _clientService.UpdateClient(persistentClient);

            return Updated(persistentClient);

        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPost]
        [SwaggerOperation(OperationId = "Clients_Create")]
        [SwaggerResponse(Status201Created, "Success", typeof(ClientWithSecrets))]
        public async Task<IActionResult> Post([FromBody] Client client)
        {

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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
                KeyType = ClientSecretType.RsaPrivateKey,
            };

            return Created(createdClient);
        }

        [HttpPut]
        [Authorize(Policy = "identity:clients:write:all")]
        [SwaggerOperation(OperationId = "Clients_NewKey")]
        [SwaggerResponse(Status200OK, "Success", typeof(ClientWithSecrets))]
        public async Task<IActionResult> NewKey([FromODataUri] string clientId)
        {
            var client = await _clientService.GetClient(clientId);
            if (client == null)
                return NotFound($"client with id {clientId} not found.");


            var privateKey = await client.NewClientCertificate();
            await _clientService.UpdateClient(client);

            var createdClient = new ClientWithSecrets
            {
                Id = client.Id,
                AllowedScopes = client.AllowedScopes,
                Description = client.Description,
                Name = client.Name,
                Key = privateKey,
                KeyType = ClientSecretType.RsaPrivateKey,
            };


            return Updated(createdClient);
        }


    }
}
