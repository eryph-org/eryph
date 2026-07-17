namespace Eryph.Configuration.Model;

public class AuthoredConfigsConfigModel
{
    public AuthoredConfigConfigModel[]? AuthoredConfigs { get; set; }
}

/// <summary>
/// The current authored value of one configuration domain at one scope, mirrored so that operator
/// input survives the re-creation of the state database.
/// </summary>
public class AuthoredConfigConfigModel
{
    /// <summary>
    /// The domain's name. A name rather than the enum, which this assembly cannot reference — and
    /// the domain is written by name everywhere else it is stored, so a value stays readable and is
    /// not invalidated by reordering the enum.
    /// </summary>
    public string? Domain { get; set; }

    public string? Scope { get; set; }

    public long Version { get; set; }

    public string? Payload { get; set; }

    public string? CreatedBy { get; set; }
}
