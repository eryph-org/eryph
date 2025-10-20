using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

public enum DeployCatletSpecificationSagaState
{
    Initiated = 0,
    DeploymentValidated = 10,
    Deployed = 20,
}
