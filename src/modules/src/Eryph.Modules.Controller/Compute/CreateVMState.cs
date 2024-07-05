namespace Eryph.Modules.Controller.Compute;

public enum CreateVMState
{
    Initiated = 0,
    ConfigValidated = 10,
    Placed = 20,
    Resolved = 30,
    Created = 40,
    Updated = 50,
}
