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

    /// <summary>Directory for client application records (one file per client id).</summary>
    public string ClientsConfigPath { get; set; } = "";

    /// <summary>Directory for redeemed enrollment-token records (one file per <c>jti</c>).</summary>
    public string RedeemedTokensConfigPath { get; set; } = "";

    // Note: OpenIddict tokens and authorizations are deliberately NOT change-tracked to files. They are
    // FK-bound runtime state (tokens/authorizations reference the application's regenerated primary key),
    // so a per-file reseed cannot preserve referential integrity. They live in the durable store and are
    // captured by a DB-level backup; on a full drop they are re-acquired by re-authentication.
}
