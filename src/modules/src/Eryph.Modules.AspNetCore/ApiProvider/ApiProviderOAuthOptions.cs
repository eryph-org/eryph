using System;
using System.Collections.Generic;
using Eryph.Core;

namespace Eryph.Modules.AspNetCore.ApiProvider;

public class ApiProviderOAuthOptions
{
    public required Uri TokenEndpoint { get; init; }

    public required IReadOnlyList<EryphConstants.Authorization.Scope> Scopes { get; init; }
}
