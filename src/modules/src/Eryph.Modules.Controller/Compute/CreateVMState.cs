namespace Eryph.Modules.Controller.Compute;

public enum CreateVMState
{
    Initiated = 0,
    ConfigValidated = 5,
    Placed = 10,
    Resolved = 12,
    ImagePrepared = 15,
    Created = 20,
    Updated = 30
}