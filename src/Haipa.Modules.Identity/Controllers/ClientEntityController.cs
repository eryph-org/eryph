namespace Haipa.Modules.Identity.Controllers
{
    using Haipa.IdentityDb;
    using Haipa.IdentityDb.Models;
    using IdentityServer4.Models;
    using Microsoft.AspNet.OData;
    using Microsoft.AspNetCore.Mvc;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
   

    /// <summary>
    /// Defines the <see cref="ClientEntityController" />
    /// </summary>
    [ApiVersion("1.0")]
    [Produces("application/json")]
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
        //[HttpPost("{description}")]
        //public IActionResult Create(string description, string secrets, string allowedScopes)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        return BadRequest();

        //    }

        //    Guid newGuid = new Guid();
        //    string clientId = newGuid.ToString();
        //    string secret = clientId;
        //    string scope = allowedScopes;

        //    var clientEntity = new ClientEntity
        //    {
        //        Client = new IdentityServer4.Models.Client
        //        {
        //            ClientName = clientId,
        //            ClientId = clientId,
        //            ClientSecrets = new List<Secret>
        //                 {
        //                      new Secret(secret.Sha256())
        //                 },
        //            Description = description,
        //            AccessTokenType = AccessTokenType.Jwt,
        //            AccessTokenLifetime = 360,
        //            IdentityTokenLifetime = 300,
        //            AllowedGrantTypes = GrantTypes.ClientCredentials,
        //            AllowedScopes = new List<string>
        //            {
        //                scope
        //            }
        //        },
        //    };

        //    clientEntity.AddDataToEntity();       
        //    _db.Clients.Add(clientEntity);
        //    _db.SaveChangesAsync();
        //    return Ok(clientEntity);
        //}
        [HttpPost("{description}")]
        public async Task<IActionResult> Post(string description, string secrets, List<string> allowedScopes)
        {
            if (!ModelState.IsValid)
            {
                return null;

            }

            Guid newGuid = Guid.NewGuid();
            string clientId = newGuid.ToString();            
            string secret = clientId;//for test

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
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    AllowOfflineAccess = true,
                    AllowedScopes = allowedScopes,
                    AllowRememberConsent = true,
                    RequireConsent = false,
                },
            };

            clientEntity.AddDataToEntity();
            _db.Clients.Add(clientEntity);
            await _db.SaveChangesAsync();
            return Created(clientEntity);
        }

        [HttpPut("{clientId}")]
        public IActionResult Update(int clientId, [FromBody]ClientEntity clientEntity)
        {
            var findResult = _db.Clients.Where(a => a.ClientId == clientId.ToString());
            if (findResult == null)
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
            _db.Entry(findResult).CurrentValues.SetValues(clientEntity);
            _db.SaveChanges();
            return Ok(findResult);
        }

        [HttpPut("{clientId}")]
        public IActionResult Delete(int clientId)
        {
            var findResult = _db.Clients.Where(a => a.ClientId == clientId.ToString());
            if (findResult == null)
            {
                return NotFound();
            }
            if (findResult.Count() > 1)
            {
                return StatusCode(409);
            }
            _db.Remove(findResult);
            return Ok(findResult);
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
    }
}
