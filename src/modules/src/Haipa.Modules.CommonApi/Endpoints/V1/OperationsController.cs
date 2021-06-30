//using System;
//using System.Collections.Generic;
//using System.Linq;
//using Haipa.Modules.AspNetCore;
//using Haipa.Modules.AspNetCore.ApiProvider;
//using Haipa.Modules.AspNetCore.ApiProvider.Model.V1;
//using Haipa.Modules.AspNetCore.OData;
//using Haipa.StateDb;
//using Microsoft.AspNetCore.Mvc;
//using Swashbuckle.AspNetCore.Annotations;
//using static Microsoft.AspNetCore.Http.StatusCodes;

//namespace Haipa.Modules.CommonApi.Controllers.V1
//{
//    [ApiVersion("1.0")]
//    [Route("[Controller]")]
//    [ApiController]
//    //[Authorize]
//    //[ApiExceptionFilter]
//    public class OperationsController : ApiController
//    {
//        private readonly StateStoreContext _db;

//        public OperationsController(StateStoreContext context)
//        {
//            _db = context;
//        }

//        [HttpGet]
//        [SwaggerOperation(OperationId = "Operations_List")]
//        [SwaggerResponse(Status200OK, "Success", typeof(ODataValueEx<IEnumerable<Operation>>))]
//        [Produces("application/json")]
//        public IActionResult Get()
//        {
//            return Ok(_db.Operations.ForMappedQuery<Operation>());
//        }

//        [HttpGet("{key}")]
//        [SwaggerOperation(OperationId = "Operations_Get")]
//        [SwaggerResponse(Status200OK, "Success", typeof(Operation))]
//        [Produces("application/json")]
//        public IActionResult Get(Guid key)
//        {
//            return Ok(_db.Operations.Where(x => x.Id == key).ForMappedQuery<Operation>());
//        }
//    }
//}