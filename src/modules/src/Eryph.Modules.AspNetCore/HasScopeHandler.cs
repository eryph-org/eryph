using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.AspNetCore;

public class HasScopeHandler : AuthorizationHandler<HasScopeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasScopeRequirement requirement)
    {
        // Search a matching scope
        var scopeClaim = context.User.FindFirst(c => c.Type == "scope" && c.Issuer == requirement.Issuer);
        // If user does not have the scope claim, get out of here
        if (scopeClaim is null)
            return Task.CompletedTask;
    
        var scopes = scopeClaim.Value.Split(' ');
        if (scopes.Any(requirement.Scopes.Contains))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
