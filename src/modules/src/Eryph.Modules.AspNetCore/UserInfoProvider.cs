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
        var claims = _contextAccessor.HttpContext?.User.Claims.ToArray() ?? Array.Empty<Claim>();
        var nameClaim = claims.FirstOrDefault(x => x.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier");
        return nameClaim?.Value ?? "";
    }

    public Guid GetUserTenantId()
    {
        var tenantId = Guid.Empty;
        var claims = _contextAccessor.HttpContext?.User.Claims.ToArray() ?? Array.Empty<Claim>();
        var tenantClaim = claims.FirstOrDefault(x => x.Type == "tid") ??
                          claims.FirstOrDefault(x => x.Type == "http://schemas.microsoft.com/identity/claims/tenantid");
        if (tenantClaim != null) _ = Guid.TryParse(tenantClaim.Value, out tenantId);

        return tenantId;
    }

    public Guid[] GetUserRoles()
    {
        //we expect multiple claims for each role 
        var rolesClaims = _contextAccessor.HttpContext?.User.FindAll(x => x.Type == ClaimTypes.Role)
                          ?? Array.Empty<Claim>();

        var roles = rolesClaims
            .Select(x => (Guid.TryParse(x.Value, out var guid), guid))
            .Where(x => x.Item1)
            .Select(x => x.guid).ToArray();

        return roles;
    }

    public AuthContext GetAuthContext()
    {
        return new AuthContext(GetUserTenantId(), new []
        {
            GetUserId()
        }, GetUserRoles());
    }
}