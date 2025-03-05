using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

internal enum PrepareCatletConfigSagaState
{
    Initiated = 0,
    ConfigValidated = 10,
    Resolved = 20,
    GenesResolved = 30,
}
