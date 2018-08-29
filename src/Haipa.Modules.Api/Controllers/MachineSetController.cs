//using System;
//using System.Linq;
//using HyperVPlus.StateDb;
//using Microsoft.AspNet.OData;
//using Microsoft.AspNetCore.Mvc;

//namespace Haipa.Api.Controllers
//{
//    public class MachineSetController : ODataController
//    {
//        private readonly StateStoreContext _db;

//        public MachineSetController(StateStoreContext context)
//        {
//            _db = context;
//        }

//        [EnableQuery]
//        public IActionResult Get()
//        {

//            return Ok(_db.Machines);
//        }
     
//        [EnableQuery]
//        public IActionResult Get(Guid key)
//        {
//            return Ok(SingleResult.Create(_db.Machines.Where(c => c.Id == key)));
//        }



//    }
//}