namespace Eryph.Modules.Controller.ChangeTracking;

public class ChangeTrackingConfig
{
    public bool TrackChanges { get; set; }

    public bool SeedDatabase { get; set; }

    public string NetworksConfigPath { get; set; } = "";

    /// <summary>
    /// Where the operator-authored configuration is mirrored. It lives only in the state database,
    /// which is re-created on a schema change, so without the mirror it is lost on every update.
    /// </summary>
    public string AuthoredConfigsPath { get; set; } = "";

    public string ProjectsConfigPath { get; set; } = "";

    public string ProjectNetworksConfigPath { get; set; } = "";

    public string ProjectNetworkPortsConfigPath { get; set; } = "";

    public string VirtualMachinesConfigPath { get; set; } = "";

    public string CatletSpecificationsConfigPath { get; set; } = "";

    public string CatletSpecificationVersionsConfigPath { get; set; } = "";
}
