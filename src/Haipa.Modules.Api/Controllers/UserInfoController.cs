using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;


namespace Haipa.Modules.Api.Controllers
{
    // ReSharper disable once StringLiteralTypo
    [Route("whoami")]
    public class UserInfoController : Controller
    {

        [Authorize]
        [HttpGet]
#pragma warning disable 1998
        public async Task<IActionResult> Get()
#pragma warning restore 1998
        {
            var subject = User.FindFirst(OpenIdConnectConstants.Claims.Subject)?.Value;
            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest();
            }

            return Content(subject);
        }
    }
}
