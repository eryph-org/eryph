namespace Eryph.Modules.Controller.Compute;

public enum CreateCatletSagaState
{
    Initiated = 0,
    ConfigPrepared = 10,
    GenesPrepared = 20,
    FodderExpanded = 30,
    Created = 40,
    Updated = 50,
}
