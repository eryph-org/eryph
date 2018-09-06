using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Modules.Api.Services;
using HyperVPlus.Messages;
using HyperVPlus.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;

namespace Haipa.Modules.Api.Controllers
{
    public class MachineSetController : ODataController
    {
        private readonly StateStoreContext _db;
        private readonly IOperationManager _operationManager;

        public MachineSetController(StateStoreContext context, IOperationManager operationManager)
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

        public IActionResult Delete(Guid key)
        {
            return Ok();
        }


        [HttpPost]
        public async Task<IActionResult> Start([FromODataUri] Guid key)
        {
           return Ok(await _operationManager.StartNew<StartVirtualMachineCommand>(key).ConfigureAwait(false));
        }

        [HttpPost]
        public async Task<IActionResult> Stop([FromODataUri] Guid key)
        {
            return Ok(await _operationManager.StartNew<StopVirtualMachineCommand>(key).ConfigureAwait(false));
        }
    }
}