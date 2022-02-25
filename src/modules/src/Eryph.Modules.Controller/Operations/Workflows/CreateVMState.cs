namespace Eryph.Modules.Controller.Operations.Workflows;

public enum CreateVMState
{
    Initiated = 0,
    ConfigValidated = 5,
    Placed = 10,
    ImagePrepared = 15,
    Created = 20,
    Updated = 30
}