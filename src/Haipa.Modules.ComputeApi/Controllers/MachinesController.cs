using System;
using System.Collections.Generic;
using Haipa.Modules.AspNetCore.ApiProvider.Services;
using Haipa.Modules.AspNetCore.OData;
using Haipa.Modules.ComputeApi.Model.V1;
using Haipa.StateDb;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Modules.AspNetCore;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.VmConfig;
using LanguageExt;
using Microsoft.AspNet.OData.Routing;
using static Microsoft.AspNetCore.Http.StatusCodes;
using Machine = Haipa.Modules.ComputeApi.Model.V1.Machine;
using Operation = Haipa.Modules.AspNetCore.ApiProvider.Model.V1.Operation;

namespace Haipa.Modules.ComputeApi.Controllers
{
    [ApiVersion( "1.0" )]
    //[Authorize]
    public class MachinesController : ApiController
    {
        private readonly StateStoreContext _db;
        private readonly IOperationManager _operationManager;

        public MachinesController(StateStoreContext context, IOperationManager operationManager)
        {
            _db = context;
            _operationManager = operationManager;
        }

        [HttpGet]
        [EnableMappedQuery]
        [SwaggerOperation( OperationId  = "Machines_List")]
        [Produces( "application/json" )]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValueEx<IEnumerable<Machine>>))]
        public IActionResult Get()
        {

            return Ok(_db.Machines.ForMappedQuery<Machine>());
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
        [EnableMappedQuery]
        public IActionResult Get([FromODataUri] string key)
        {
            return Ok(SingleResult.Create(_db.Machines.Where(c => c.Id == Convert.ToInt64(key)).ForMappedQuery<Machine>()));
        }

        [HttpDelete]
        [SwaggerOperation(OperationId = "Machines_Delete")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public Task<IActionResult> Delete([FromODataUri] string key)
        {
            return FindMachine(key).MapAsync(id =>
                    Accepted(_operationManager.StartNew<DestroyResourcesCommand>(
                            new Resource(ResourceType.Machine, Convert.ToInt64(id))
                            )
                    )).ToAsync()
                .Match(r => r, l => l);

        }


        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Create")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        [ODataRoute("CreateMachine")]

        public async Task<IActionResult> Create([FromBody] MachineProvisioningSettings settings)
        {
            var machineConfig = settings.Configuration.ToObject<MachineConfig>();

            return Accepted(await _operationManager.StartNew(
                new CreateMachineCommand
                {
                    CorrelationId = settings.CorrelationId,
                    Config = machineConfig,
                }
                ).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Update")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public async Task<IActionResult> Update([FromODataUri] string key, [FromBody] MachineProvisioningSettings settings)
        {
            var machine = _db.Machines.FirstOrDefault(op => op.Id == Convert.ToInt64(key));

            if (machine == null)
                return NotFound();

            var machineConfig = settings.Configuration.ToObject<MachineConfig>();


            return Accepted(await _operationManager.StartNew(
                new UpdateMachineCommand
                {
                    CorrelationId = settings.CorrelationId,
                    MachineId = Convert.ToInt64(key),
                    Config = machineConfig,
                    AgentName = machine.AgentName,
                }
            ).ConfigureAwait(false));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Start")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public Task<IActionResult> Start([FromODataUri] string key)
        {
            return FindMachine(key).MapAsync(id =>
                    Accepted(_operationManager.StartNew<StartMachineCommand>(
                            new Resource(ResourceType.Machine, Convert.ToInt64(id))))
                    ).ToAsync()
                .Match(r => r, l => l);
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "Machines_Stop")]
        [SwaggerResponse(Status202Accepted, "Success", typeof(Operation))]
        [Produces("application/json")]
        public Task<IActionResult> Stop([FromODataUri] string key)
        {
            return FindMachine(key).MapAsync(id =>
                    Accepted(_operationManager.StartNew<StopMachineCommand>(
                        new Resource(ResourceType.Machine, Convert.ToInt64(id))))
                    ).ToAsync()
                .Match(r => r, l => l);
        }


        private async Task<Either<IActionResult,long>> FindMachine(string key)
        {
            var vm = await _db.FindAsync<StateDb.Model.Machine>(Convert.ToInt64(key));
            if (vm == null)
                return NotFound();

            return vm.Id;
        }
    }
}