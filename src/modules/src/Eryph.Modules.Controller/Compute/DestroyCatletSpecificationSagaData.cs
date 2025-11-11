using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

internal class DestroyCatletSpecificationSagaData
{
    public DestroyCatletSpecificationSagaState State { get; set; }

    public Guid SpecificationId { get; set; }
}
