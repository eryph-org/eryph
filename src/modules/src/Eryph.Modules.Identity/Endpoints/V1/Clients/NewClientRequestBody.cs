using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.Identity.Models.V1;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

public class NewClientRequestBody : IAllowedScopesHolder
{
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
    public IReadOnlyList<string>? Roles { get; set; }
}
