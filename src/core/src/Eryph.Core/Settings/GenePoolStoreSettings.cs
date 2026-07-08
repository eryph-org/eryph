namespace Eryph.Core.Settings;

/// <summary>
/// Gene pool node settings — where this node stores genes. This is the node-local <b>cache</b> of the
/// controller-distributed storage config: the gene pool derives its root from the same central storage
/// config the agent uses (the default volumes path plus a <c>genepool</c> folder), which the
/// storage-config realizer writes here and <c>GenePoolPathProvider</c> reads — mirroring how the agent
/// caches its config in <c>agentsettings.yml</c>. Not operator-owned; a manual edit is overwritten on
/// the next storage-config push.
/// </summary>
public sealed class GenePoolStoreSettings
{
    /// <summary>
    /// Filesystem path of the gene pool datastore on this node — where genes are downloaded and kept.
    /// </summary>
    public string Path { get; set; } = "";
}
