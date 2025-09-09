using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public static class AuthorizationPolicyBuilderExtensions
{
    /// <summary>
    /// Configures the <see cref="AuthorizationPolicyBuilder"/> to require
    /// that the user is authenticated unless the accessed endpoint belongs
    /// to the OpenAPI documentation.
    /// </summary>
    public static AuthorizationPolicyBuilder RequireAuthenticatedUserOrSwaggerEndpoint(
        this AuthorizationPolicyBuilder builder) =>
        builder.RequireAssertion(context =>
            context.Resource is HttpContext httpContext && httpContext.Request.Path.StartsWithSegments("/swagger")
            || context.User.Identity?.IsAuthenticated == true);
}
