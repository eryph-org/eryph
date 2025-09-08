namespace Eryph.Modules.Controller.Compute;

internal enum BuildCatletSpecificationSagaState
{
    Initiated = 0,
    ConfigBuilt = 10,
    GenesResolved = 20,
    Expanded = 30,
}
