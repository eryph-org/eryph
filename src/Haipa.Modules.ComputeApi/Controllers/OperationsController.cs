using System;
using System.Collections.Generic;
using System.Linq;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.ComputeApi.Controllers
{
    [ApiVersion( "1.0" )]
    public class OperationsController : ODataController
    {
        private readonly StateStoreContext _db;

        public OperationsController(StateStoreContext context)
        {
            _db = context;
        }

        [EnableQuery]
        [HttpGet]
        [SwaggerOperation(OperationId = "Operations_List")]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValue<IEnumerable<Operation>>))]
        [Produces("application/json")]
        public IActionResult Get()
        {

            return Ok(_db.Operations);
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "Operations_Get")]
        [SwaggerResponse(Status200OK, "Success", typeof(Operation))]
        [Produces("application/json")]
        [EnableQuery]
        public IActionResult Get(Guid key)
        {
            return Ok(SingleResult.Create(_db.Operations.Where(c => c.Id == key)));
        }


        [EnableQuery]
        [SwaggerOperation(OperationId = "Operations_GetLogEntries")]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValue<IEnumerable<OperationLogEntry>>))]
        [Produces("application/json")]
        public IActionResult GetLogEntries([FromODataUri] Guid key)
        {
            var op = _db.Operations.FirstOrDefault(x => x.Id == key);
            if (op == null) return Ok();

            return Ok(_db.Logs.Where(x => x.Operation == op));
        }



    }
}