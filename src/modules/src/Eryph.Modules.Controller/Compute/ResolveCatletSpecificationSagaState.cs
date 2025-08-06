namespace Eryph.Modules.Controller.Compute;

internal enum ResolveCatletSpecificationSagaState
{
    Initiated = 0,
    Resolved = 10,
    GenesResolved = 20,
    Expanded = 30,
}
