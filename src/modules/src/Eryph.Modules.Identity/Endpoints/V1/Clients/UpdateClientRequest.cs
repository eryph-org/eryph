using Eryph.Modules.Identity.Models.V1;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

public class UpdateClientRequest
{
    [FromRoute(Name = "id")] public required string Id { get; set; }

    [FromBody] public required UpdateClientRequestBody Client { get; set; }
}

public class UpdateClientRequestBody : IAllowedScopesHolder
{
    /// <summary>
    /// Human-readable name of the client, for example email address of owner.
    /// </summary>
    [MaxLength(254)]
    public required string Name { get; set; }

    /// <inheritdoc/>
    public required IReadOnlyList<string> AllowedScopes { get; set; }
}
