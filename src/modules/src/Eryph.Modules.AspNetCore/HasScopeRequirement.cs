using System;
using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.AspNetCore
{
    public class HasScopeRequirement : IAuthorizationRequirement
    {
        public HasScopeRequirement(string issuer, params string[] scopes)
        {
            Scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
            Issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
        }

        public string Issuer { get; }
        public string[] Scopes { get; }
    }
}