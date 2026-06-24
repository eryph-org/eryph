using System.Net;
using System.Security.Claims;
using Eryph.Core;

namespace Eryph.Modules.AspNetCore.TestBase;

public static class HttpClientExtensions
{
    extension(HttpClient client)
    {
        public HttpClient SetEryphToken(Guid tenantId,
            Guid identityId,
            string scope,
            bool isSuperAdmin) =>
            client.SetEryphToken(tenantId, identityId.ToString(), scope, isSuperAdmin);

        public HttpClient SetEryphToken(Guid tenantId,
            string identityId,
            string scope,
            bool isSuperAdmin)
        {
            client.SetFakeBearerToken(new Dictionary<string, object>
            {
                ["iss"] = "fake",
                ["sub"] = identityId,
                ["scope"] = scope,
                ["tid"] = tenantId,
                [ClaimTypes.Role] = isSuperAdmin ? EryphConstants.SuperAdminRole : Guid.NewGuid(),
            });

            return client;
        }
    }
}
