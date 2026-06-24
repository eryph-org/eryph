using System;

namespace Eryph.Modules.Controller.Compute;

internal class DestroyCatletSpecificationSagaData
{
    public DestroyCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }
}
