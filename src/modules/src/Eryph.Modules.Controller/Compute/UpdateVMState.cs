namespace Eryph.Modules.Controller.Compute;

public enum UpdateVMState
{
    Initiated = 0,
    ConfigValidated = 10,
    Resolved = 20,
    GenesPrepared = 30,
    VMUpdated = 40,
    ConfigDriveUpdated = 50,
    NetworksUpdated = 60,
}