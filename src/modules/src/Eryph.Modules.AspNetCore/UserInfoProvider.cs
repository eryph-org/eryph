using System;
using System.Linq;
using System.Security.Claims;
using Eryph.Core;
using Microsoft.AspNetCore.Http;

namespace Eryph.Modules.AspNetCore;

public class UserInfoProvider : IUserInfoProvider
{
    private readonly IHttpContextAccessor _contextAccessor;

    public UserInfoProvider(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public string GetUserId()
    {
        var principal = GetClaimsPrincipal();
        
        var userId = principal.FindFirstValue("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        if (string.IsNullOrEmpty(userId))
            throw new InvalidOperationException("The authenticated principal does not contain a user ID.");

        return userId;
    }

    public Guid GetUserTenantId()
    {
        var principal = GetClaimsPrincipal();

        var tenantId = principal.FindFirstValue("tid")
                       ?? principal.FindFirstValue("http://schemas.microsoft.com/identity/claims/tenantid");

        if (!Guid.TryParse(tenantId, out var validTenantId))
            throw new InvalidOperationException("The authenticated principal does not contain a tenant ID.");

        return validTenantId;
    }

    public Guid[] GetUserRoles()
    {
        var principal = GetClaimsPrincipal();
        
        // we expect multiple claims for each role 
        var rolesClaims = principal.FindAll(ClaimTypes.Role);

        var roles = rolesClaims
            .Select(c => Guid.TryParse(c.Value, out var guid) ? (Guid?) guid : null)
            .Where(r => r.HasValue)
            .Select(r => r!.Value)
            .ToArray();

        return roles;
    }

    public AuthContext GetAuthContext()
    {
        return new AuthContext(GetUserTenantId(), [GetUserId()], GetUserRoles());
    }

    private ClaimsPrincipal GetClaimsPrincipal()
    {
        if (_contextAccessor.HttpContext is null)
            throw new InvalidOperationException("The HttpContext is missing.");
        
        return _contextAccessor.HttpContext.User;
        
    }
}
