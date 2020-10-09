using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Haipa.Messages.Commands.OperationTasks;
using Haipa.Modules.ApiProvider.Services;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.VmConfig;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.ComputeApi.Controllers
{
    [ApiVersion( "1.0" )]
    [Authorize]
    public class VirtualDisksController : ODataController
    {
        private readonly StateStoreContext _db;
        private readonly IOperationManager _operationManager;

        public VirtualDisksController(StateStoreContext context, IOperationManager operationManager)
        {
            _db = context;
            _operationManager = operationManager;
        }

        [HttpGet]
        [SwaggerOperation( OperationId  = "VirtualDisks_List")]
        [Produces( "application/json" )]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValue<IEnumerable<Machine>>))]
        [EnableQuery(MaxExpansionDepth = 3)]
        public IActionResult Get()
        {

            return Ok(_db.VirtualDisks);
        }

        /// <summary>
        /// Gets a single machine.
        /// </summary>
        /// <response code="200">The machine was successfully retrieved.</response>
        /// <response code="404">The machine does not exist.</response>
        [HttpGet]
        [SwaggerOperation(OperationId = "VirtualDisks_Get")]
        [Produces("application/json")]
        [SwaggerResponse(Status200OK, "Success", typeof(Machine))]
        [EnableQuery()]
        public IActionResult Get([FromODataUri] Guid key)
        {
            return Ok(SingleResult.Create(_db.VirtualDisks.Where(c => c.Id == key)));
        }

    }
}