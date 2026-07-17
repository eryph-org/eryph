namespace Eryph.Modules.Controller.ChangeTracking.AuthoredConfigs;

/// <summary>
/// The operator-authored configuration changed and must be mirrored to disk.
/// </summary>
/// <remarks>
/// Authored configuration is operator input which lives only in the state database. eryph-zero
/// deletes and re-seeds that database whenever the schema changes, so without a mirror every
/// authored value is lost on an update — the environments and the sites they are realized by among
/// them, which the resources are pinned to.
/// </remarks>
internal record AuthoredConfigChange;
