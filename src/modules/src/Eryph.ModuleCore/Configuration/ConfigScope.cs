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

    /// <summary>The maximum length of a scope selector (matches the <c>Scope</c> column width).</summary>
    public const int MaxLength = 255;

    // env names and tag keys are eryph identifiers: case-insensitive, so canonicalize to lower case
    // (mirrors CatletConfigNormalizer/EryphName) and trim, so authoring and resolution agree ordinally.
    private static string NormalizeIdentifier(string value) => value.Trim().ToLowerInvariant();

    public static string ForEnvironment(string environment) =>
        $"env:{NormalizeIdentifier(environment)}";

    // Both key and value are normalized (trim + lower-case). Lower-casing the whole selector keeps
    // matching consistent across providers — MariaDB's default case-insensitive collation and SQLite's
    // binary comparison agree once every stored scope is already lower-case — without pinning a column
    // collation. The key must not contain '=' (it would make the selector ambiguous with the value),
    // which is enforced at the metadata boundary.
    public static string ForTag(string key, string value) =>
        $"tag:{NormalizeIdentifier(key)}={NormalizeIdentifier(value)}";

    // Guid.ToString("D") is the canonical lower-case, hyphenated form.
    public static string ForHost(Guid componentId) => $"host:{componentId:D}";

    /// <summary>
    /// Normalizes an operator-supplied scope selector to its canonical form (the exact string used for
    /// storage and resolution), or reports why it is malformed. Canonicalization — not mere validation —
    /// is what makes a resolvable scope: it lower-cases env/tag identifiers and reformats a host GUID so
    /// an operator typo like <c>env:Prod</c> or <c>host:{GUID}</c> resolves instead of silently never
    /// matching. Called at the authoring boundary before the value is stored.
    /// </summary>
    public static bool TryCanonicalize(string? scope, out string canonical, out string? error)
    {
        canonical = Default;
        error = null;

        if (string.IsNullOrWhiteSpace(scope))
            return true; // default / match-all

        scope = scope.Trim();

        if (scope.StartsWith("env:", StringComparison.Ordinal))
        {
            var name = scope["env:".Length..].Trim();
            if (string.IsNullOrEmpty(name))
            {
                error = "An environment scope requires a non-empty environment name.";
                return false;
            }

            canonical = ForEnvironment(name);
            return canonical.Length <= MaxLength || TooLong(out error);
        }

        if (scope.StartsWith("host:", StringComparison.Ordinal))
        {
            if (!Guid.TryParse(scope["host:".Length..], out var componentId))
            {
                error = "A host scope requires a valid component id (GUID).";
                return false;
            }

            canonical = ForHost(componentId);
            return true;
        }

        if (scope.StartsWith("tag:", StringComparison.Ordinal))
        {
            var rest = scope["tag:".Length..];
            var separator = rest.IndexOf('=');
            if (separator <= 0)
            {
                error = "A tag scope must be 'tag:key=value'.";
                return false;
            }

            var key = rest[..separator].Trim();
            var value = rest[(separator + 1)..].Trim();
            if (!IsValidTagKey(key, out error))
                return false;

            canonical = ForTag(key, value);
            return canonical.Length <= MaxLength || TooLong(out error);
        }

        error = $"'{scope}' is not a valid configuration scope (expected env:, tag: or host:).";
        return false;

        bool TooLong(out string? tooLongError)
        {
            tooLongError = $"The scope selector must be at most {MaxLength} characters.";
            return false;
        }
    }

    /// <summary>
    /// Whether a tag key is well-formed: non-empty and free of the selector delimiters. A key containing
    /// <c>=</c> would collide two distinct tags onto one selector (<c>tag:a=b=c</c>), so it is rejected
    /// both here and at the component-metadata boundary.
    /// </summary>
    public static bool IsValidTagKey(string? key, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            error = "A tag key must not be empty.";
            return false;
        }

        if (key.IndexOfAny(['=', ':']) >= 0 || key.Any(char.IsWhiteSpace))
        {
            error = "A tag key must not contain '=', ':' or whitespace.";
            return false;
        }

        return true;
    }

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
    public static bool IsValid(string? scope) => TryCanonicalize(scope, out _, out _);
}
