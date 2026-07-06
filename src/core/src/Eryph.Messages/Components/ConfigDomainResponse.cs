namespace Eryph.Messages.Components;

/// <summary>
/// The reply to <see cref="GetConfigDomainCommand"/>: the domain's current authored version and
/// payload, or nulls when nothing has been authored yet (the domain still falls back to its file/derived
/// default).
/// </summary>
public class ConfigDomainResponse
{
    public ConfigDomain Domain { get; set; }

    public long? Version { get; set; }

    public string? Payload { get; set; }
}
