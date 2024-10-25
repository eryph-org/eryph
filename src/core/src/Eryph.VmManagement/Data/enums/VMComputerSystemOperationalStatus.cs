using System;

namespace Eryph.VmManagement.Data;

public enum VMComputerSystemOperationalStatus
{
    Ok = 2,
    Degraded = 3,
    PredictiveFailure = 5,
    InService = 11,
    Dormant = 15,
}
