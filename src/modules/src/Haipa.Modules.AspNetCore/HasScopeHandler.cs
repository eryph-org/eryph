using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Haipa.Modules.AspNetCore
{
    public class HasScopeHandler : AuthorizationHandler<HasScopeRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            HasScopeRequirement requirement)
        {
            // If user does not have the scope claim, get out of here
            if (!context.User.HasClaim(c => c.Type == "scope" && c.Issuer == requirement.Issuer))
                return Task.CompletedTask;

            // search a matching scope
            if (context.User.FindFirst(c => c.Type == "scope"
                                            && requirement.Scopes.Contains(c.Value)
                                            && c.Issuer == requirement.Issuer) != null)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}