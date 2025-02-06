namespace Eryph.Modules.Controller.Compute;

public enum UpdateCatletSagaState
{
    Initiated = 0,
    ConfigPrepared = 10,
    GenesPrepared = 20,
    VMUpdated = 30,
    ConfigDriveUpdated = 40,
    NetworksUpdated = 50,
}
