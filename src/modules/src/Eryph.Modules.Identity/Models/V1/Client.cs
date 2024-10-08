using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

[PublicAPI]
public class Client : IAllowedScopesHolder
{
    /// <summary>
    /// The Unique identifier of the eryph client.
    /// Only characters a-z, A-Z, numbers 0-9 and hyphens are allowed.
    /// </summary>
    [MaxLength(40)]
    public required string Id { get; set; }

    /// <summary>
    /// Human-readable name of the client, for example email address of owner.
    /// </summary>
    [MaxLength(254)]
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required IReadOnlyList<string> AllowedScopes { get; set; }

    /// <summary>
    /// The roles of the client,
    /// </summary>
    public required IReadOnlyList<string> Roles { get; set; }

    /// <summary>
    /// The ID of the tenant to which the client belongs.
    /// </summary>
    public required string TenantId { get; set; }
}
