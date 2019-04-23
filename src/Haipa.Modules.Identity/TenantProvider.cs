using Haipa.IdentityDb;
using Microsoft.AspNetCore.Http;

namespace Haipa.Modules.Identity
{
    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
            => _httpContextAccessor = httpContextAccessor;

        public string GetCurrentTenant()
        {
            if (_httpContextAccessor.HttpContext == null || 
                !_httpContextAccessor.HttpContext.Items.TryGetValue("tenantId", out var tenant) 
                || string.IsNullOrEmpty(tenant?.ToString()))
            {
                tenant = "default";
            }

            return tenant.ToString();
        }
    }
}