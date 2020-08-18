using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    public class ClientsController : ODataController
    {
        private readonly IClientService<ClientApiModel> _clientService;
        public ClientsController(IClientService<ClientApiModel> clientService)
        {
            _clientService = clientService;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Clients_List")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ODataValue<DbSet<ClientApiModel>>), Status200OK)]
        [ProducesResponseType(Status404NotFound)]
        [EnableQuery]
        public IQueryable<ClientApiModel> Get()
        {
            return _clientService.QueryClients();
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Clients_Get")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(ClientApiModel), Status200OK)]
        [ProducesResponseType(Status404NotFound)]
        public async Task<IActionResult> Get([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);

            if (client == null) return NotFound();

            return Ok(client);
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpDelete]
        [SwaggerOperation(OperationId = "Clients_Delete")]
        [ProducesResponseType(Status200OK)]
        [Produces("application/json")]
        public async Task<IActionResult> Delete([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);
            if (client == null) return NotFound();

            await _clientService.DeleteClient(client);

            return Ok();
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPut]
        [SwaggerOperation(OperationId = "Clients_Update")]
        [ProducesResponseType(Status200OK)]
        [ProducesResponseType(Status400BadRequest)]
        [Produces("application/json")]
        public Task<IActionResult> Put([FromODataUri] string key, [FromBody] Delta<ClientApiModel> client)
        {
            return PutOrPatch(key, client, true);
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPatch]
        [SwaggerOperation(OperationId = "Clients_Change")]
        [ProducesResponseType(Status200OK)]
        [ProducesResponseType(Status400BadRequest)]
        [Produces("application/json")]
        public Task<IActionResult> Patch([FromODataUri] string key, [FromBody] Delta<ClientApiModel> client)
        {
            return PutOrPatch(key, client,false);
        }


        private async Task<IActionResult> PutOrPatch([FromODataUri] string clientId, Delta<ClientApiModel> client, bool putMode)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            
            var persistentClient = await _clientService.GetClient(clientId);
            if (client == null) return NotFound();

            if(putMode)
                client.Put(persistentClient);
            else
                client.Patch(persistentClient);

            persistentClient.Id = clientId;
            await _clientService.UpdateClient(persistentClient);

            return Ok();
            
        }

        [Authorize(Policy = "identity:clients:write:all")]
        [HttpPost]
        [SwaggerOperation(OperationId = "Clients_Create")]
        [ProducesResponseType(typeof(ClientApiModel), Status201Created)]
        [ProducesResponseType(Status400BadRequest)]
        [Produces("application/json")]
        public async Task<IActionResult> Post([FromBody] ClientApiModel client)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            client.Id = Guid.NewGuid().ToString();

            await _clientService.AddClient(client);
            return Created(client);
        }
    }
}
