using System;

namespace Eryph.Modules.Controller.Compute;

public class StopCatletSagaData
{
    public Guid CatletId { get; set; }

    public Guid VmId { get; set; }
}
