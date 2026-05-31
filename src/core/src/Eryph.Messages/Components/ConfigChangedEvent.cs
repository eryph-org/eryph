namespace Eryph.Messages.Components;

/// <summary>
/// Announces that a configuration domain advanced to a new version. Published by
/// the controller so components can notice they have drifted. (In the initial
/// pilot the controller also pushes the new bundle directly to live components
/// via <see cref="PushConfigCommand"/>; this event is the broadcast counterpart.)
/// </summary>
public class ConfigChangedEvent
{
    public ConfigDomain Domain { get; set; }

    public long NewVersion { get; set; }
}
