using System;
using System.Collections.Generic;
using System.Linq;
using Eryph.StateDb.Model;

namespace Eryph.ModuleCore.Configuration;

/// <summary>
/// Configuration scope selectors. A scope selects which components an authored value targets. The
/// selector is a single string: <c>""</c> (default / match-all), <c>env:&lt;name&gt;</c>,
/// <c>tag:&lt;key&gt;=&lt;value&gt;</c> or <c>host:&lt;componentId&gt;</c>. A component resolves the
/// most-specific authored value among its scopes, precedence <c>host &gt; tag &gt; env &gt; default</c>.
/// </summary>
public static class ConfigScope
{
    /// <summary>The default (match-all) scope; lowest precedence.</summary>
    public const string Default = "";

    public static string ForEnvironment(string environment) => $"env:{environment}";

    public static string ForTag(string key, string value) => $"tag:{key}={value}";

    public static string ForHost(Guid componentId) => $"host:{componentId}";

    /// <summary>
    /// The scopes a component can resolve to, most-specific first, always ending in
    /// <see cref="Default"/>. Config resolution picks the authored value at the first of these that
    /// has one. Tags are ordered deterministically (by key) so resolution is stable.
    /// </summary>
    public static IReadOnlyList<string> ResolutionOrder(ComponentRegistration registration)
    {
        var scopes = new List<string> { ForHost(registration.ComponentId) };
        foreach (var tag in registration.Tags.OrderBy(t => t.Key, StringComparer.Ordinal))
            scopes.Add(ForTag(tag.Key, tag.Value));
        if (!string.IsNullOrEmpty(registration.Environment))
            scopes.Add(ForEnvironment(registration.Environment));
        scopes.Add(Default);
        return scopes;
    }

    /// <summary>Whether a component resolves the given scope (i.e. it is one of its selectors).</summary>
    public static bool Matches(string scope, ComponentRegistration registration) =>
        ResolutionOrder(registration).Contains(scope);

    /// <summary>
    /// Whether a string is a well-formed scope selector. Used to reject a malformed scope at the
    /// authoring boundary before it is stored (an unparseable scope could never be resolved).
    /// </summary>
    public static bool IsValid(string? scope)
    {
        if (string.IsNullOrEmpty(scope))
            return true; // default / match-all

        if (scope.StartsWith("env:", StringComparison.Ordinal))
            return !string.IsNullOrWhiteSpace(scope["env:".Length..]);

        if (scope.StartsWith("host:", StringComparison.Ordinal))
            return Guid.TryParse(scope["host:".Length..], out _);

        if (scope.StartsWith("tag:", StringComparison.Ordinal))
        {
            var rest = scope["tag:".Length..];
            var separator = rest.IndexOf('=');
            // Non-whitespace key (value may be empty). A whitespace-only key could never match a real
            // tag and would just create a permanently-unresolvable scope, so reject it here.
            return separator > 0 && !string.IsNullOrWhiteSpace(rest[..separator]);
        }

        return false;
    }
}
