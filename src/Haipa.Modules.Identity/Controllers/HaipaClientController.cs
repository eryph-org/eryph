using Haipa.IdentityDb;
using Haipa.StateDb;
using Microsoft.AspNet.OData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.AspNetCore.Mvc;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
using Microsoft.AspNet.OData.Routing;
using HttpGetAttribute = Microsoft.AspNetCore.Mvc.HttpGetAttribute;
using Haipa.Modules.Identity.IdentityServer;
using IdentityServer4.Models;
using System.Security.Claims;
using Haipa.Modules.Identity.Demo;

namespace Haipa.Modules.Identity.Controllers
{
    
    [ApiVersion("1.0")]
    //[System.Web.Http.Route("/v{version:apiVersion}/HaipaClient")]
    //[ApiController]
    [Produces("application/json")]

    public class HaipaClientController : ODataController
    {    
        private readonly  ConfigurationStoreContext _db;
        public HaipaClientController( ConfigurationStoreContext context2)
        {
         
            _db = context2;
        }     
        [HttpGet]
        public async Task<ActionResult> Get()
        {
            _db.AddRange(Config.GetClients());
            _db.AddRange(Config.GetIdentityResources());
            _db.AddRange(Config.GetApiResources());
            _db.SaveChanges();

            return null;
        }        
    
        //public async Task<IHttpActionResult> Delete(ApplicationUser user)
        //{
        //    _db.Users.Add(new ApplicationUser { UserName = "test", Email = "test@test.com" });
        
        //    await _db.SaveChangesAsync();
        //    return null;
        //}
        //[EnableQuery]
        //public IQueryable<ApplicationUser> Get()
        //{
        //    return _db.Users;
        //}
        //[EnableQuery]
        //public Microsoft.AspNet.OData.SingleResult<ApplicationUser> Get([FromODataUri] string key)
        //{
        //    IQueryable<ApplicationUser> result = _db.Users.Where(p => p.Id == key);
        //    return Microsoft.AspNet.OData.SingleResult.Create(result);
        //}

    }
}
