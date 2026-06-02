namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Configures the identity change-tracking / config-export pipeline (the identity analog of the state
/// store's change tracking). <see cref="TrackChanges"/> mirrors every committed change to the on-disk
/// config files; <see cref="SeedDatabase"/> rebuilds the database from those files on startup. In
/// eryph-zero both are on (the SQLite store is disposable and rebuilt from files); in server mode they
/// are off by default and switched on only to take a backup or perform a live ("flying") DB migration.
/// </summary>
public class IdentityChangeTrackingConfig
{
    public bool TrackChanges { get; set; }

    public bool SeedDatabase { get; set; }

    /// <summary>Directory for redeemed enrollment-token records (one file per <c>jti</c>).</summary>
    public string RedeemedTokensConfigPath { get; set; } = "";

    /// <summary>Directory for OpenIddict token records (one file per token id).</summary>
    public string TokensConfigPath { get; set; } = "";

    /// <summary>Directory for OpenIddict authorization records (one file per authorization id).</summary>
    public string AuthorizationsConfigPath { get; set; } = "";
}
