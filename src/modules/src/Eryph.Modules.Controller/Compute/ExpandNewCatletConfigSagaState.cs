using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

internal enum ExpandNewCatletConfigSagaState
{
    Initiated = 0,
    ConfigPrepared = 10,
    GenesPrepared = 20,
    FodderExpanded = 30,
}
