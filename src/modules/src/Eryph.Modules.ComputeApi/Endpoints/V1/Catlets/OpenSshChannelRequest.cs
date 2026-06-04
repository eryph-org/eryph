using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Request for the SSH channel control endpoint. <see cref="SingleEntityRequest.Id"/> carries the
/// catlet id (route). The optional added-key flow supplies the operator's public key and a TTL.
/// </summary>
public class OpenSshChannelRequest : SingleEntityRequest
{
    /// <summary>
    /// Optional operator public key (OpenSSH authorized_keys form) to authorize in the guest. When
    /// omitted, no key is written (the pre-injected-key flow).
    /// </summary>
    [FromQuery(Name = "publicKey")]
    public string? PublicKey { get; set; }

    /// <summary>
    /// Optional time-to-live in seconds for the injected key. Only applies when
    /// <see cref="PublicKey"/> is supplied.
    /// </summary>
    [FromQuery(Name = "ttl")]
    public int? Ttl { get; set; }
}
