using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace Eryph.Modules.AspNetCore;

public class HasScopeRequirement(
    string issuer,
    params string[] scopes)
    : IAuthorizationRequirement
{
    public string Issuer { get; } = issuer;

    public IReadOnlySet<string> Scopes { get; } = scopes.ToHashSet();
}
