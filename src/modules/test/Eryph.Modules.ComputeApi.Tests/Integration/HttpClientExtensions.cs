using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;

namespace Eryph.Modules.ComputeApi.Tests.Integration;

public static class HttpClientExtensions
{
    public static HttpClient SetEryphToken(
        this HttpClient client,
        Guid tenantId,
        Guid identityId,
        string scope,
        bool isSuperAdmin) =>
        client.SetEryphToken(tenantId, identityId.ToString(), scope, isSuperAdmin);

    public static HttpClient SetEryphToken(
        this HttpClient client,
        Guid tenantId,
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
            [ClaimTypes.Role] = isSuperAdmin ? EryphConstants.SuperAdminRole : Guid.NewGuid()
        });

        return client;
    }
}
