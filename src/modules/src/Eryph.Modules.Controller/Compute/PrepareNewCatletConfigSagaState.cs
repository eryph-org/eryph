using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

public enum PrepareNewCatletConfigSagaState
{
    Initiated = 0,
    ConfigValidated = 10,
    Placed = 20,
    Resolved = 30,
    GenesResolved = 35,
    Created = 40,
    Updated = 50,
}