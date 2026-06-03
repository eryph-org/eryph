using System;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class AddAccessKeyRequest : SingleEntityRequest
{
    [FromBody]
    public required AddAccessKeyRequestBody Body { get; set; }
}

public class AddAccessKeyRequestBody
{
    /// <summary>The operator's SSH public key in OpenSSH authorized_keys form.</summary>
    public required string PublicKey { get; set; }

    /// <summary>Optional ISO 8601 duration (informational); the server applies <see cref="ExpiresAt"/>.</summary>
    public string? Ttl { get; set; }

    /// <summary>When the key should stop authorizing. Null = no expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
}
