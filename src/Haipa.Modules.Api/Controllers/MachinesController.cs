using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.Api.Controllers
{
    [ApiVersion( "1.0" )]
    public class MachinesController : ODataController
    {
        private readonly StateStoreContext _db;
        private readonly IOperationManager _operationManager;

        public MachinesController(StateStoreContext context, IOperationManager operationManager)
        {
            _db = context;
            _operationManager = operationManager;
        }

        [HttpGet]
        [SwaggerOperation( OperationId  = "Machines_List")]
        [Produces( "application/json" )]
        [ProducesResponseType( typeof(ODataValue<DbSet<Machine>> ), Status200OK )]
        [ProducesResponseType( Status404NotFound )]
        [EnableQuery]
        public IActionResult Get()
        {

            return Ok(_db.Machines);
        }

        /// <summary>
        /// Gets a single machine.
        /// </summary>
        /// <response code="200">The machine was successfully retrieved.</response>
        /// <response code="404">The machine does not exist.</response>
        [HttpGet]
        [SwaggerOperation(OperationId = "Machines_Get")]
        [Produces("application/json")]
        [ProducesResponseType( typeof( Machine), Status200OK )]
        [ProducesResponseType( Status404NotFound )]
        [EnableQuery]
        public IActionResult Get(Guid key)
        {
            return Ok(SingleResult.Create(_db.Machines.Where(c => c.Id == key)));
        }

        [HttpDelete]
        [SwaggerOperation(OperationId = "Machines_Delete")]
        [ProducesResponseType(typeof(Operation), Status200OK)]
        [Produces("application/json")]
        public async Task<IActionResult> Delete(Guid key)
        {
            return Ok(await _operationManager.StartNew<DestroyMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_UpdateOrCreate")]
        [ProducesResponseType(typeof(Operation), Status200OK)]
        [Produces("application/json")]
        public async Task<IActionResult> Post([FromBody] MachineConfig config)
        {

            return Ok(await _operationManager.StartNew(
                new CreateOrUpdateMachineCommand
                {
                    Config = config,
                }
                ).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Start")]
        [ProducesResponseType(typeof(Operation), Status200OK)]
        [Produces("application/json")]
        public async Task<IActionResult> Start([FromODataUri] Guid key)
        {
           return Ok(await _operationManager.StartNew<StartMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Stop")]
        [ProducesResponseType(typeof(Operation), Status200OK)]
        [Produces("application/json")]
        public async Task<IActionResult> Stop([FromODataUri] Guid key)
        {
            return Ok(await _operationManager.StartNew<StopMachineCommand>(key).ConfigureAwait(false));
        }
    }
}