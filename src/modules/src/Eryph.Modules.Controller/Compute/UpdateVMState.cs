namespace Eryph.Modules.Controller.Compute;

public enum UpdateVMState
{
    Initiated = 0,
    ConfigPrepared = 27,
    GenesPrepared = 30,
    VMUpdated = 40,
    ConfigDriveUpdated = 50,
    NetworksUpdated = 60,
}
