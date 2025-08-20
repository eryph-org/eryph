using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Controller.Compute;

public enum DeployCatletSagaState
{
    Initiated = 0,
    VmCreated = 10,
    CatletNetworksUpdated = 20,
    VmUpdated = 30,
    ConfigDriveUpdated = 40,
    NetworksUpdated = 50,
}
