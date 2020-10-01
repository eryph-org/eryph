using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Modules.Api.Services;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
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
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValue<IEnumerable<Machine>>))]
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
        [SwaggerResponse(Status200OK, "Success", typeof(Machine))]
        [EnableQuery]
        public IActionResult Get([FromODataUri] Guid key)
        {
            return Ok(SingleResult.Create(_db.Machines.Where(c => c.Id == key)));
        }

        [HttpDelete]
        [SwaggerOperation(OperationId = "Machines_Delete")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Delete([FromODataUri] Guid key)
        {
            return Accepted(await _operationManager.StartNew<DestroyMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Create")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Post([FromBody] MachineConfig config)
        {

            return Accepted(await _operationManager.StartNew(
                new CreateMachineCommand
                {
                    Config = config,
                }
                ).ConfigureAwait(false));
        }

        [HttpPut]
        [SwaggerOperation(OperationId = "Machines_Update")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Put([FromODataUri] Guid key, [FromBody] MachineConfig config)
        {
            var machine = _db.Machines.FirstOrDefault(op => op.Id == key);

            if (machine == null)
                return NotFound();
            
            return Accepted(await _operationManager.StartNew(
                new UpdateMachineCommand
                {
                    MachineId = key,
                    Config = config,
                    AgentName = machine.AgentName,
                }
            ).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Start")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Start([FromODataUri] Guid key)
        {
           return Accepted(await _operationManager.StartNew<StartMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Stop")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Stop([FromODataUri] Guid key)
        {
            return Accepted(await _operationManager.StartNew<StopMachineCommand>(key).ConfigureAwait(false));
        }
    }
}