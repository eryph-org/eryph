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
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using LanguageExt;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.ComputeApi.Controllers
{
    [ApiVersion( "1.0" )]
    [ApiExceptionFilter]
    //[Authorize]
    public class VirtualMachinesController : ApiController
    {
        private readonly StateStoreContext _db;

        public VirtualMachinesController(StateStoreContext context)
        {
            _db = context;
        }

        [HttpGet]
        [EnableMappedQuery]
        [SwaggerOperation( OperationId  = "VirtualMachines_List")]
        [Produces( "application/json" )]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValueEx<IEnumerable<VirtualMachine>>))]
        public IActionResult Get()
        {

            return Ok(_db.VirtualMachines.ForMappedQuery<VirtualMachine>());
        }

        /// <summary>
        /// Gets a single machine.
        /// </summary>
        /// <response code="200">The machine was successfully retrieved.</response>
        /// <response code="404">The machine does not exist.</response>
        [HttpGet]
        [SwaggerOperation(OperationId = "VirtualMachines_Get")]
        [Produces("application/json")]
        [SwaggerResponse(Status200OK, "Success", typeof(VirtualMachine))]
        [EnableMappedQuery]
        public IActionResult Get([FromODataUri] string key)
        {
            return Ok(SingleResult.Create(_db.VirtualMachines.Where(c => c.Id == Convert.ToInt64(key)).ForMappedQuery<VirtualMachine>()));
        }

    }
}