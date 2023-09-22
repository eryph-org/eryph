using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Eryph.Modules.Identity.Models.V1;

[PublicAPI]
public class Client
{
    /// <summary>
    ///     Unique identifier for a eryph client
    ///     Only characters a-z, A-Z, numbers 0-9 and hyphens are allowed.
    /// </summary>
    [Key]
    [MaxLength(40)]
    public string Id { get; set; }

    /// <summary>
    ///     human readable name of client, for example email address of owner
    /// </summary>
    [MaxLength(254)]
    public string Name { get; set; }

    /// <summary>
    ///     allowed scopes of client
    /// </summary>
    public string[] AllowedScopes { get; set; }


    /// <summary>
    ///     Roles of client
    /// </summary>
    public Guid[] Roles { get; set; }

    /// <summary>
    ///    Tenant of client
    /// </summary>
    public Guid TenantId { get; set; }


}