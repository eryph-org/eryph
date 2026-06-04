using System;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class SshChannelOperationResult : OperationResult
{
    public required string Token { get; set; }

    public required DateTimeOffset ExpiresAt { get; set; }
}
