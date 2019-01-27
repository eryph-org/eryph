using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.Modules.Api.Services;
using Haipa.StateDb;
using Haipa.VmConfig;
using JetBrains.Annotations;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Haipa.Modules.Api.Controllers
{
    public class MachinesController : ODataController
    {
        private readonly StateStoreContext _db;
        private readonly IOperationManager _operationManager;

        public MachinesController(StateStoreContext context, IOperationManager operationManager)
        {
            _db = context;
            _operationManager = operationManager;
        }

        [EnableQuery]
        public IActionResult Get()
        {

            return Ok(_db.Machines);
        }

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