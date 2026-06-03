using System;

namespace Eryph.Modules.Controller.Compute;

public class AddSshKeySagaData
{
    public Guid VmId { get; set; }

    public string SubjectId { get; set; }
}
