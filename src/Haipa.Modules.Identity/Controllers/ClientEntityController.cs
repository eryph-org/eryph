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

    /// <summary>
    /// Defines the <see cref="ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [EnableCors("CorsPolicy")]
    public class ClientEntityController : ODataController
    {
        /// <summary>
        /// Defines the _db
        /// </summary>
        private readonly ConfigurationStoreContext _db;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientEntityController"/> class.
        /// </summary>
        /// <param name="context">The context<see cref="ConfigurationStoreContext"/></param>
        public ClientEntityController(ConfigurationStoreContext context)
        {
            _db = context;
        }

        /// <summary>
        /// The Get
        /// </summary>
        /// <returns>The <see cref="IQueryable{ClientEntity}"/></returns>
        [EnableQuery]
        public IQueryable<ClientEntity> Get()
        {
            return _db.Clients;
        }

        /// <summary>
        /// The Delete
        /// </summary>
        /// <param name="key">The key<see cref="Guid"/></param>
        /// <returns>The <see cref="Task{ActionResult}"/></returns>
        public async Task<ActionResult> Delete([FromODataUri] Guid key)
        {
            var client = _db.Clients.Where(i => i.ClientId == key);
            if (client.Count() > 1)
            {
                return BadRequest();
            }
            if (client.Count() == 0)
            {
                return NotFound();
            }
            var c = client.First();
            if (c.ConfigFileExists())
            {
                c.DeleteFile();
            }
            _db.Clients.Remove(c);
            await _db.SaveChangesAsync();
            return StatusCode((int)HttpStatusCode.NoContent);
        }

        /// <summary>
        /// The Put
        /// </summary>
        /// <param name="key">The key<see cref="Guid"/></param>
        /// <param name="description">The description<see cref="string"/></param>
        /// <param name="secret">The secret<see cref="string"/></param>
        /// <param name="allowedScopes">The allowedScopes<see cref="List{string}"/></param>
        /// <returns>The <see cref="Task{ActionResult}"/></returns>
        public async Task<ActionResult> Put([FromODataUri] Guid key, string description, [Required] string secret, [Required] List<string> allowedScopes)
        {
            string clientId = key.ToString();
            var findResult = _db.Clients.Where(a => a.ClientId == key);
            if (findResult.Count() == 0)
            {
                return NotFound();
            }
            if (findResult.Count() > 1)
            {
                return StatusCode(409);
            }
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }
            var c = findResult.First();
            var tempConfigFile = c.ConfigFile;

            var clientEntity = new ClientEntity
            {
                Client = new IdentityServer4.Models.Client
                {
                    ClientName = clientId,
                    ClientId = clientId,
                    ClientSecrets = new List<Secret>
                    {
                        new Secret(secret.Sha256())
                    },
                    Description = description,
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = allowedScopes,
                    AllowRememberConsent = true,
                    RequireConsent = false,
                },
                ConfigFile = tempConfigFile
            };
            clientEntity.AddDataToEntity();
            _db.Entry(c).CurrentValues.SetValues(clientEntity);
            c.MapDataFromEntity();
            _db.SaveChanges();
            if (c.ConfigFileExists())
            {
                c.UpdateToFile();
            }
            return Ok(c);
        }

        /// <summary>
        /// The Post
        /// </summary>
        /// <param name="description">The description<see cref="string"/></param>
        /// <param name="secret">The secret<see cref="string"/></param>
        /// <param name="allowedScopes">The allowedScopes<see cref="List{string}"/></param>
        /// <param name="saveAsFile">The saveAsFile<see cref="Boolean"/></param>
        /// <returns>The <see cref="Task{IActionResult}"/></returns>
        [HttpPost]
        public async Task<IActionResult> Post(string description, [Required] string secret, [Required] List<string> allowedScopes, Boolean saveAsFile = false)
        {
            if (!ModelState.IsValid)
            {
                return null;

            }

            Guid newGuid = Guid.NewGuid();
            string clientId = newGuid.ToString();

            var clientEntity = new ClientEntity
            {
                Client = new IdentityServer4.Models.Client
                {
                    ClientName = clientId,
                    ClientId = clientId,
                    ClientSecrets = new List<Secret>
                         {
                              new Secret(secret.Sha256())
                         },
                    Description = description,
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = allowedScopes,
                    AllowRememberConsent = true,
                    RequireConsent = false,
                },
            };

            clientEntity.AddDataToEntity();
            if (saveAsFile) clientEntity.SaveToFile();
            _db.Clients.Add(clientEntity);
            await _db.SaveChangesAsync();
            return Created(clientEntity);
        }
    }
}
