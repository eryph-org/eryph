using System.Collections.Generic;
using Eryph.Core;

namespace Eryph.ModuleCore.Authorization;

/// <summary>
/// Centralized definitions of scopes organized by module to avoid duplication
/// across ComputeApiModule, IdentityModule, and tests.
/// </summary>
public static class ScopeDefinitions
{
    /// <summary>
    /// All scopes that need policies in the Compute API module.
    /// These scopes control access to compute resources like catlets, genes, and projects.
    /// </summary>
    public static readonly string[] ComputeApiScopes =
    [
        EryphConstants.Authorization.Scopes.CatletsRead,
        EryphConstants.Authorization.Scopes.CatletsWrite,
        EryphConstants.Authorization.Scopes.CatletsControl,
        EryphConstants.Authorization.Scopes.GenesRead,
        EryphConstants.Authorization.Scopes.GenesWrite,
        EryphConstants.Authorization.Scopes.ProjectsRead,
        EryphConstants.Authorization.Scopes.ProjectsWrite,
    ];

    /// <summary>
    /// All scopes that need policies in the Identity API module.
    /// These scopes control access to identity management resources.
    /// </summary>
    public static readonly string[] IdentityApiScopes =
    [
        EryphConstants.Authorization.Scopes.IdentityClientsRead,
        EryphConstants.Authorization.Scopes.IdentityClientsWrite,
    ];

}