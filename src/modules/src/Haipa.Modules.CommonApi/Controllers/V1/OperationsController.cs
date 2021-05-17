using System;
using System.Collections.Generic;
using System.Linq;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
using Haipa.Modules.AspNetCore.OData;
using Haipa.StateDb;
using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace Haipa.Modules.CommonApi.Controllers.V1
{
    [ApiVersion( "1.0" )]
    //[Authorize]
    [ApiExceptionFilter]
    public class OperationsController : ODataController
    {
        private readonly StateStoreContext _db;

        public OperationsController(StateStoreContext context)
        {
            _db = context;
        }

        [HttpGet]
        [EnableMappedQuery]
        [SwaggerOperation(OperationId = "Operations_List")]
        [SwaggerResponse(Status200OK, "Success", typeof(ODataValueEx<IEnumerable<Operation>>))]
        [Produces("application/json")]
        public IActionResult Get()
        {
            return Ok(_db.Operations.ForMappedQuery<Operation>());
        }

        [HttpGet]
        [EnableMappedQuery]
        [SwaggerOperation(OperationId = "Operations_Get")]
        [SwaggerResponse(Status200OK, "Success", typeof(Operation))]
        [Produces("application/json")]
        public IActionResult Get(Guid key)
        {
            return Ok(SingleResult.Create(_db.Operations.Where(x=>x.Id == key).ForMappedQuery<Operation>()));
        }


    }
}