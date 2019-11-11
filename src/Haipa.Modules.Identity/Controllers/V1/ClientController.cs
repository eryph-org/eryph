using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Identity.Models.V1;
using Haipa.Modules.Identity.Services;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.Identity.Controllers.V1
{
    /// <inheritdoc />
    /// <summary>
    /// Defines the <see cref="!:ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [EnableCors("CorsPolicy")]
    public class ClientsController : ODataController
    {
        private readonly IClientService<ClientApiModel> _clientService;
        public ClientsController(IClientService<ClientApiModel> clientService)
        {
            _clientService = clientService;
        }

        [EnableQuery]
        public IQueryable<ClientApiModel> Get()
        {
            return _clientService.QueryClients();
        }

        public async Task<IActionResult> Get([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);

            if (client == null) return NotFound();

            return Ok(client);
        }

        public async Task<IActionResult> Delete([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);
            if (client == null) return NotFound();

            await _clientService.DeleteClient(client);

            return Ok();
        }

        public Task<IActionResult> Put([FromODataUri] string key, [FromBody] Delta<ClientApiModel> client)
        {
            return PutOrPatch(key, client, true);
        }

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


            await _clientService.UpdateClient(persistentClient);

            return Ok();
            
        }

        public async Task<IActionResult> Post([FromBody] ClientApiModel client)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var persistentClient = await _clientService.GetClient(client.ClientId);
            if (persistentClient != null)
                return Conflict();

            await _clientService.AddClient(client);
            return Created(client);
        }
    }
}
