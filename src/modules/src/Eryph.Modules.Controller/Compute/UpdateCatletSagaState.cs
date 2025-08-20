namespace Eryph.Modules.Controller.Compute;

public enum UpdateCatletSagaState
{
    Initiated = 0,
    SpecificationBuilt = 10,
    DeploymentValidated = 20,
    Deployed = 30
}
