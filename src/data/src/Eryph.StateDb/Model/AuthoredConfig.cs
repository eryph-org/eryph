using System;
using Eryph.Messages.Components;

namespace Eryph.StateDb.Model;

/// <summary>
/// One immutable, operator-authored version of a configuration domain, scoped to a slice of the
/// customer landscape. Authored config is the counterpart to the system-derived
/// <see cref="ConfigRecord"/>: instead of being built from controller state, its payload is set by an
/// operator (via the management API) and kept as a version history so the UI can show, diff and roll
/// back changes.
/// </summary>
/// <remarks>
/// The current value for a (<see cref="Domain"/>, <see cref="Scope"/>) pair is the entry with the
/// highest <see cref="Version"/>; older entries are retained as history. Because versions are
/// immutable, a rollback is simply a new version carrying an earlier payload — there is no mutable
/// "is current" flag to keep consistent.
///
/// <see cref="Scope"/> selects which components receive the value: the empty string is the default
/// (match-all) scope used by <c>Global</c> domains and as the fallback for <c>Scopable</c> domains;
/// a non-empty value is a canonical environment/tag selector. There is at most one entry per
/// (<see cref="Domain"/>, <see cref="Scope"/>, <see cref="Version"/>).
/// </remarks>
public class AuthoredConfig
{
    public Guid Id { get; set; }

    public ConfigDomain Domain { get; set; }

    /// <summary>The landscape selector; empty string is the default (match-all) scope.</summary>
    public required string Scope { get; set; }

    /// <summary>Monotonic per (<see cref="Domain"/>, <see cref="Scope"/>); the highest is current.</summary>
    public long Version { get; set; }

    public required string Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The authoring principal, for audit/UI; null when set by an unattributed path (import).</summary>
    public string? CreatedBy { get; set; }
}
