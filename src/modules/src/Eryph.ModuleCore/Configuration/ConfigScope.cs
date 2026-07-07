namespace Eryph.ModuleCore.Configuration;

/// <summary>
/// Well-known configuration scopes. A scope selects which components receive an authored value.
/// </summary>
public static class ConfigScope
{
    /// <summary>
    /// The default (match-all) scope, used by <c>Global</c> domains and as the fallback for
    /// <c>Scopable</c> domains.
    /// </summary>
    public const string Default = "";
}
