namespace Eryph.Modules.Controller.Compute;

internal enum ValidateCatletSpecificationSagaState
{
    Initiated = 0,
    SpecificationBuilt = 10,
    Completed = 20,
}
