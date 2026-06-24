using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>
/// Request for the SSH channel data-plane (connect) endpoint. <see cref="SingleEntityRequest.Id"/>
/// carries the catlet id (route); <see cref="Token"/> is the one-time channel token the client read
/// from the completed <c>OpenSshChannel</c> operation result.
/// </summary>
public class SshChannelConnectRequest : SingleEntityRequest
{
    [FromQuery(Name = "token")] public string Token { get; set; } = "";
}
