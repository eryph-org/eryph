namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

/// <summary>Shared bounds for the SSH channel/key endpoints.</summary>
internal static class SshChannelLimits
{
    /// <summary>Max public-key length; the authorized_keys line must also fit the Hyper-V KVP value limit.</summary>
    public const int MaxPublicKeyLength = 2048;

    /// <summary>Max key lifetime (30 days) so a key cannot be authorized effectively forever.</summary>
    public const int MaxTtlSeconds = 30 * 24 * 60 * 60;
}
