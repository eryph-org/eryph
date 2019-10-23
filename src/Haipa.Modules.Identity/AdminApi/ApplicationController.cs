using Microsoft.AspNet.OData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Haipa.Modules.Identity.Controller
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("adminapi/v{version:apiVersion}/applications")]

    public class ApplicationController : ControllerBase
    {

        //[Authorize("identity:apps:read:all")]
        //public async Task<IActionResult> Get()
        //{
        //    //var r = await _manager.ListAsync(q => q.Include(x => x.Authorizations));
        //    //return new JsonResult(r.Select(MapReadModel));

        //    return new JsonResult(null);
        //}


        //    [Route("{id}")]
        //    [Authorize("identity:apps:read:all")]
        //    public async Task<IActionResult> Get(string id)
        //    {
        //        var r = await _manager.FindByClientIdAsync(id);
        //        if (r == null)
        //            return NotFound();

        //        return new JsonResult(MapReadModel(r));
        //    }

        //    private static ApplicationReadModel MapReadModel(OpenIddictApplication app)
        //    {
        //        var permissions = JsonConvert.DeserializeObject<string[]>(app.Permissions);

        //        return new ApplicationReadModel
        //        {
        //            ClientId = app.ClientId,
        //            DisplayName = app.DisplayName,
        //            GrantTypes = MapPermissionsToGrantType(permissions).Select(gt => gt.ToString()),
        //            Scopes = permissions.Where(p=>p.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope))
        //                     .Select(x=>x.Substring(OpenIddictConstants.Permissions.Prefixes.Scope.Length)),
        //            PostLogoutRedirectUris = app.PostLogoutRedirectUris,
        //            RedirectUris = app.RedirectUris
        //        };
        //    }

        //    private static IEnumerable<GrantType> MapPermissionsToGrantType(string[] permissions)
        //    {
        //        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials))
        //            yield return GrantType.ClientCredentials;
        //        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.Implicit))
        //            yield return GrantType.Implicit;
        //        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.Password))
        //            yield return GrantType.Password;
        //        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode))
        //            yield return GrantType.AuthorizationCode;
        //        if (permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.RefreshToken))
        //            yield return GrantType.RefreshToken;
        //    }
        //}
    }

    //[ApiVersion("1.0")]

    //public class PersonsController : ControllerBase
    //{
    //    [EnableQuery]
    //    [HttpGet]
    //    public IQueryable<Person> Get()
    //    {
    //        return new string[] { "Alice", "Bob", "Chloe", "Dorothy", "Emma", "Fabian", "Garry", "Hannah" }
    //            .Select(v => new Person { Name = v, ClientId = Guid.NewGuid() })
    //            .AsQueryable();
    //    }

    //    public class Person
    //    {
    //        [Key]
    //        public int Id { get; set; }
    //        public Guid ClientId { get; set; }
    //        public string Name { get; set; }
    //    }
    //}
    
}
