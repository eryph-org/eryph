using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.Core;

namespace Eryph.ModuleCore.Authorization;

public static class ScopeHierarchy
{
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
    /// Gets all scopes that are implied by the given scope, including the scope itself.
    /// </summary>
    /// <param name="scope">The scope to expand</param>
    /// <returns>A collection of all implied scopes</returns>
    public static IEnumerable<string> GetImpliedScopes(string? scope)
    {
        if (string.IsNullOrEmpty(scope))
            yield break;

        // Always include the scope itself
        yield return scope;

        // Add any implied scopes
        foreach (var impliedScope in impliedScopes)
        {
            yield return impliedScope;
        }
    }

    /// <summary>
    /// Expands a collection of scopes to include all implied scopes.
    /// </summary>
    /// <param name="scopes">The scopes to expand</param>
    /// <returns>A distinct collection of all scopes including implied ones</returns>
    public static IEnumerable<string> ExpandScopes(IEnumerable<string>? scopes)
    {
        if (scopes == null)
            return [];

        return scopes
            .SelectMany(GetImpliedScopes)
            .Distinct(StringComparer.Ordinal);
    }

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
        return expandedGrantedScopes.Contains(requestedScope, StringComparer.Ordinal);
    }

    /// <summary>
    /// Validates that all requested scopes are allowed given the granted scopes.
    /// </summary>
    /// <param name="requestedScopes">The scopes being requested</param>
    /// <param name="grantedScopes">The scopes that have been granted to the client</param>
    /// <returns>True if all requested scopes are allowed, false otherwise</returns>
    public static bool AreAllScopesAllowed(IEnumerable<string>? requestedScopes, IEnumerable<string>? grantedScopes)
    {
        if (requestedScopes == null)
            return true;

        return grantedScopes != null && requestedScopes.All(scope => IsScopeAllowed(scope, grantedScopes));
    }

    /// <summary>
    /// Gets all scopes that should be granted when a specific scope is assigned to a client.
    /// This includes the scope itself and all scopes it implies.
    /// </summary>
    /// <param name="assignedScopes">The scopes assigned to the client</param>
    /// <returns>All scopes that should be available, including implied ones</returns>
    public static IEnumerable<string> GetAvailableScopes(IEnumerable<string> assignedScopes)
    {
        return ExpandScopes(assignedScopes);
    }
}