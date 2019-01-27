using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Haipa.Modules.Api.Controllers
{
    public class NetworksController : ODataController
    {
        private readonly StateStoreContext _db;

        public NetworksController(StateStoreContext context)
        {
            _db = context;
        }

        private bool NetworkExists(Guid key)
        {
            return _db.Networks.Any(p => p.Id == key);
        }

        [EnableQuery]
        public IActionResult Get()
        {

            return Ok(_db.Networks);
        }
        

        [EnableQuery]
        public IActionResult Get(Guid key)
        {
            return Ok(SingleResult.Create(_db.Networks.Where(c => c.Id == key)));
        }

        public async Task<IActionResult> Post([FromBody] Network network)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            _db.Networks.Add(network);
            await _db.SaveChangesAsync();
            return Created(network);
        }
        

        public async Task<IActionResult> Patch([FromODataUri] Guid key, Delta<Network> product)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var entity = await _db.Networks.FindAsync(key);
            if (entity == null)
            {
                return NotFound();
            }
            product.Patch(entity);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NetworkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return Updated(entity);
        }
        public async Task<IActionResult> Put([FromODataUri] Guid key, [FromBody] Network update)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (key != update.Id)
            {
                return BadRequest();
            }
            _db.Entry(update).State = EntityState.Modified;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!NetworkExists(key))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return Updated(update);
        }
    }
}