using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

public enum ValidateCatletDeploymentSagaState
{
    Initiated = 0,
    GenesPrepared = 10,
    NetworkValidated = 30,
}
