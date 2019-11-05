namespace Haipa.Modules.Identity.Controllers
{
    using Haipa.IdentityDb;
    using Haipa.IdentityDb.Models;
    using Haipa.IdentityDb.Extensions;
    using IdentityServer4.Models;
    using Microsoft.AspNet.OData;
    using Microsoft.AspNetCore.Cors;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Haipa.IdentityDb.Services;
    using Haipa.IdentityDb.Dtos;

    /// <summary>
    /// Defines the <see cref="ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [EnableCors("CorsPolicy")]
    public class ClientEntityController : ODataController
    {
        private readonly ConfigurationStoreContext _db;
        private readonly ClientEntityService _clientEntityService;
        public ClientEntityController(ClientEntityService clientEntityService)
        {
            _clientEntityService = clientEntityService;
        }

        [EnableQuery]
        public IQueryable<ClientEntityDTO> Get()
        {
            return _clientEntityService.GetClient();
        }
        public async Task<ActionResult> Delete([FromODataUri] Guid clientId)
        { 
            int r = await _clientEntityService.DeleteClient(clientId);
            return  new JsonResult(r);
        }
        public async Task<ActionResult> Put(ClientEntityDTO client)
        {
            int r = await _clientEntityService.PutClient(client);
            return new JsonResult(r);            
        }
        [HttpPost]
        public async Task<IActionResult> Post(ClientEntityDTO client)
        {
           int r = await _clientEntityService.PostClient(client);
            return new JsonResult(r);            
        }
    }
}
