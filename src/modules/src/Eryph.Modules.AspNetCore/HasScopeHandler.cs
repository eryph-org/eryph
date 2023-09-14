using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.AspNetCore
{
    public class HasScopeHandler : AuthorizationHandler<HasScopeRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HasScopeHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            HasScopeRequirement requirement)
        {
            // search a matching scope
            var scopeClaim = context.User.FindFirst(c => c.Type == "scope" && c.Issuer == requirement.Issuer);
            // If user does not have the scope claim, get out of here
            if(scopeClaim == null)
                return Task.CompletedTask;
    
            var scopes = scopeClaim.Value.Split(' ');


            if(scopes.Any(scope => requirement.Scopes.Contains(scope)))
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}