namespace Eryph.Messages.Components;

/// <summary>
/// The reply to <see cref="SetConfigDomainCommand"/>: whether the value was accepted and stored (with
/// its new version), or rejected with a reason. Replying — rather than failing the message — gives the
/// management API immediate feedback and avoids retrying a permanently-invalid write.
/// </summary>
public class SetConfigDomainResponse
{
    public bool Success { get; set; }

    public long? Version { get; set; }

    public string? Error { get; set; }
}
