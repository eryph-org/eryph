using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.Core;

namespace Eryph.ModuleCore.Authorization;

public static class ScopeHierarchy
{
    /// <summary>
    /// WARNING: This scope hierarchy is FLATTENED, not transitive.
    /// Each parent scope must explicitly list ALL child scopes it grants access to.
    /// The system does NOT automatically resolve multiple levels of hierarchy.
    /// 
    /// For example, if you want ComputeWrite to grant access to CatletsRead,
    /// you must explicitly list CatletsRead in ComputeWrite's scope list.
    /// Do NOT rely on transitive relationships like ComputeWrite -> ComputeRead -> CatletsRead.
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> ScopeHierarchyList = new()
    {
        // Compute API hierarchies
        [EryphConstants.Authorization.Scopes.ComputeWrite] =
        [
            EryphConstants.Authorization.Scopes.ComputeRead,
            EryphConstants.Authorization.Scopes.CatletsWrite,
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl,
            EryphConstants.Authorization.Scopes.GenesWrite,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.ProjectsWrite,
            EryphConstants.Authorization.Scopes.ProjectsRead
        ],
        [EryphConstants.Authorization.Scopes.ComputeRead] =
        [
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.GenesRead,
            EryphConstants.Authorization.Scopes.ProjectsRead
        ],
        [EryphConstants.Authorization.Scopes.CatletsWrite] =
        [
            EryphConstants.Authorization.Scopes.CatletsRead,
            EryphConstants.Authorization.Scopes.CatletsControl
        ],
        [EryphConstants.Authorization.Scopes.CatletsControl] =
        [
            EryphConstants.Authorization.Scopes.CatletsRead
        ],
        [EryphConstants.Authorization.Scopes.GenesWrite] =
        [
            EryphConstants.Authorization.Scopes.GenesRead
        ],
        [EryphConstants.Authorization.Scopes.ProjectsWrite] =
        [
            EryphConstants.Authorization.Scopes.ProjectsRead
        ],

        // Identity API hierarchies
        [EryphConstants.Authorization.Scopes.IdentityWrite] =
        [
            EryphConstants.Authorization.Scopes.IdentityRead,
            EryphConstants.Authorization.Scopes.IdentityClientsWrite,
            EryphConstants.Authorization.Scopes.IdentityClientsRead
        ],
        [EryphConstants.Authorization.Scopes.IdentityRead] =
        [
            EryphConstants.Authorization.Scopes.IdentityClientsRead
        ],
        [EryphConstants.Authorization.Scopes.IdentityClientsWrite] =
        [
            EryphConstants.Authorization.Scopes.IdentityClientsRead
        ],
    };

    /// <summary>
    /// Gets all scopes that are implied by the given <paramref name="scope"/>,
    /// including the <paramref name="scope"/> itself.
    /// </summary>
    /// <param name="scope">The scope to expand</param>
    /// <returns>A set of all implied scopes (including the <paramref name="scope"/> itself)</returns>
    public static ISet<string> GetImpliedScopes(string? scope) =>
        string.IsNullOrEmpty(scope) || !ScopeHierarchyList.TryGetValue(scope, out var impliedScopes)
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(impliedScopes, StringComparer.Ordinal);

    /// <summary>
    /// Expands a collection of scopes to include all implied scopes.
    /// </summary>
    /// <param name="scopes">The scopes to expand</param>
    /// <returns>A set of all scopes including implied ones (no duplicates)</returns>
    public static ISet<string> ExpandScopes(IEnumerable<string> scopes) =>
        scopes.SelectMany(GetImpliedScopes).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Checks if a requested scope is allowed given the granted scopes, considering scope hierarchy.
    /// </summary>
    /// <param name="requestedScope">The scope being requested</param>
    /// <param name="grantedScopes">The scopes that have been granted to the client</param>
    /// <returns>True if the requested scope is allowed, false otherwise</returns>
    public static bool IsScopeAllowed(string? requestedScope, IEnumerable<string>? grantedScopes)
    {
        if (string.IsNullOrEmpty(requestedScope))
            return false;

        // Expand all granted scopes to include their implied scopes
        var expandedGrantedScopes = ExpandScopes(grantedScopes);

        // Check if the requested scope is in the expanded granted scopes
        return expandedGrantedScopes.Contains(requestedScope);
    }

    /// <summary>
    /// Gets all scopes that grant access to the given <paramref name="scope"/>.
    /// This includes the scope itself and any higher-level scopes that include it.
    /// This method performs the inverse operation of <see cref="GetImpliedScopes"/>.
    /// </summary>
    /// <param name="scope">The scope to find granting scopes for</param>
    /// <returns>An array of all scopes that grant access to the specified scope</returns>
    public static string[] GetGrantingScopes(string scope)
    {
        if (string.IsNullOrEmpty(scope))
            return [];

        // Start with the scope itself
        var grantingScopes = new List<string> { scope };

        // Find all scopes in the hierarchy that grant the given scope
        foreach (var (parentScope, impliedScopes) in ScopeHierarchyList)
        {
            if (impliedScopes.Contains(scope) && !grantingScopes.Contains(parentScope))
            {
                grantingScopes.Add(parentScope);
            }
        }

        return grantingScopes.ToArray();
    }
}
