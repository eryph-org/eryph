//using System;
//using System.Linq;
//using Haipa.StateDb;
//using Microsoft.AspNet.OData;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Swashbuckle.AspNetCore.Annotations;

//namespace Haipa.Modules.ComputeApi.Controllers
//{
//    [ApiVersion( "1.0" )]
//    [Authorize]
//    public class SubnetsController : ODataController
//    {
//        private readonly StateStoreContext _db;

//        public SubnetsController(StateStoreContext context)
//        {
//            _db = context;
//        }

//        private bool SubnetExists(Guid key)
//        {
//            return _db.Subnets.Any(p => p.Id == key);
//        }

//        [EnableQuery]
//        [SwaggerOperation(OperationId = "Subnets_List")]
//        public IActionResult Get()
//        {

//            return Ok(_db.Subnets);
//        }


//        [EnableQuery]
//        [SwaggerOperation(OperationId = "Subnets_Get")]
//        public IActionResult Get(Guid key)
//        {
//            return Ok(SingleResult.Create(_db.Subnets.Where(c => c.Id == key)));
//        }

//    }
//}