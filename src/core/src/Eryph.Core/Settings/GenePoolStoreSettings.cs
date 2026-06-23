namespace Eryph.Core.Settings;

/// <summary>
/// Gene pool node settings — the gene-pool counterpart to the agent's <c>agentsettings.yml</c> and the
/// controller's <c>controllersettings.yml</c>. It holds where this node stores genes, split out from the
/// agent's settings so the gene pool owns its own storage configuration instead of borrowing the agent's
/// datastore.
///
/// This is the <b>local</b> copy of that configuration. Mid-term the controller owns and distributes it
/// (groupable per environment, so independent environments can resolve their gene store differently) and
/// each node keeps a local copy — mirroring how the NetworkProviders / Placement domains are modelled.
/// Only the local file is read today; distribution is not implemented yet.
/// </summary>
public sealed class GenePoolStoreSettings
{
    /// <summary>
    /// Filesystem path of the gene pool datastore on this node — where genes are downloaded and kept.
    /// </summary>
    public string Path { get; set; } = "";
}
