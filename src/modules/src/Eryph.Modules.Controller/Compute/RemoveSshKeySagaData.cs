using System;

namespace Eryph.Modules.Controller.Compute;

public class RemoveSshKeySagaData
{
    public Guid VmId { get; set; }

    public string SubjectId { get; set; }
}
