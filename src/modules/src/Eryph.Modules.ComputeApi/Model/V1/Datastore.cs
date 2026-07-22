namespace Eryph.Modules.ComputeApi.Model.V1;

/// <summary>
/// A datastore name from the deployment's storage vocabulary. Provided as an option list so clients can
/// offer the selectable datastores when authoring configuration. Only the name is exposed — the
/// filesystem paths a datastore maps to are agent-local and not part of this catalog.
/// </summary>
public class Datastore
{
    /// <summary>The datastore name.</summary>
    public required string Name { get; set; }
}
