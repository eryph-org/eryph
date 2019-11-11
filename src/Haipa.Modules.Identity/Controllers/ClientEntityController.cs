using Haipa.Modules.Identity.Models;
using Haipa.Modules.Identity.Services;

namespace Haipa.Modules.Identity.Controllers
{
    using Microsoft.AspNet.OData;
    using Microsoft.AspNetCore.Cors;
    using Microsoft.AspNetCore.Mvc;
    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Defines the <see cref="ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [EnableCors("CorsPolicy")]
    public class ClientEntityController : ODataController
    {
        private readonly IClientService _clientService;
        public ClientEntityController(IClientService clientService)
        {
            _clientService = clientService;
        }

        [EnableQuery]
        public IQueryable<ClientEntityDTO> Get()
        {
            return _clientService.QueryClients();
        }

        public async Task<ActionResult> Get([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);

            if (client == null) return NotFound();

            return Ok(client);
        }

        public async Task<ActionResult> Delete([FromODataUri] string key)
        {
            var client = await _clientService.GetClient(key);
            if (client == null) return NotFound();

            await _clientService.DeleteClient(client);

            return Ok();
        }

        public Task<ActionResult> Put([FromODataUri] string key, [FromBody] Delta<ClientEntityDTO> client)
        {
            return PutOrPatch(key, client, true);
        }

        public Task<ActionResult> Patch([FromODataUri] string key, [FromBody] Delta<ClientEntityDTO> client)
        {
            return PutOrPatch(key, client,false);
        }


        private async Task<ActionResult> PutOrPatch([FromODataUri] string clientId, Delta<ClientEntityDTO> client, bool putMode)
        {
            var persistentClient = await _clientService.GetClient(clientId);
            if (client == null) return NotFound();

            if(putMode)
                client.Put(persistentClient);
            else
                client.Patch(persistentClient);


            await _clientService.UpdateClient(persistentClient);

            return Ok();
            
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] ClientEntityDTO client)
        {
            var persistentClient = await _clientService.GetClient(client.ClientId);
            if (persistentClient != null)
                return Conflict();

            await _clientService.AddClient(client);
            return Created(client);
        }
    }
}
