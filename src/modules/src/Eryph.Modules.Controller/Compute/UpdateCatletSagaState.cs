namespace Eryph.Modules.Controller.Compute;

public enum UpdateCatletSagaState
{
    Initiated = 0,
    ConfigPrepared = 10,
    GenesPrepared = 20,
    FodderExpanded = 30,
    CatletNetworksUpdated = 40,
    VMUpdated = 50,
    ConfigDriveUpdated = 60,
    NetworksUpdated =70,
}
