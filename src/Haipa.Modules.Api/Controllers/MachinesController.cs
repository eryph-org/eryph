using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Microsoft.AspNet.OData;
using Microsoft.AspNet.OData.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Microsoft.AspNet.OData.Query.AllowedQueryOptions;
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

        [Produces( "application/json" )]
        [ProducesResponseType( typeof( DbSet<Machine> ), Status200OK )]
        [ProducesResponseType( Status404NotFound )]
        [EnableQuery]
        public IActionResult Get()
        {

            return Ok(_db.Machines);
        }

        /// <summary>
        /// Gets a single order.
        /// </summary>
        /// <response code="200">The order was successfully retrieved.</response>
        /// <response code="404">The order does not exist.</response>
        [Produces( "application/json" )]
        [ProducesResponseType( typeof( Machine), Status200OK )]
        [ProducesResponseType( Status404NotFound )]
        [EnableQuery]
        public IActionResult Get(Guid key)
        {
            return Ok(SingleResult.Create(_db.Machines.Where(c => c.Id == key)));
        }

        public async Task<IActionResult> Delete(Guid key)
        {
            return Ok(await _operationManager.StartNew<DestroyMachineCommand>(key).ConfigureAwait(false));
        }

        public async Task<IActionResult> Post([FromBody] MachineConfig config)
        {

            return Ok(await _operationManager.StartNew(
                new ConvergeVirtualMachineCommand
                {
                    Config = config,
                }
                ).ConfigureAwait(false));
        }

        [HttpPost]
        public async Task<IActionResult> Start([FromODataUri] Guid key)
        {
           return Ok(await _operationManager.StartNew<StartMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        public async Task<IActionResult> Stop([FromODataUri] Guid key)
        {
            return Ok(await _operationManager.StartNew<StopMachineCommand>(key).ConfigureAwait(false));
        }
    }
}