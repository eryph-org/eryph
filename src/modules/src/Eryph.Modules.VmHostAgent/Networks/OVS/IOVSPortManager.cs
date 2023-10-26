using System;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Networks.OVS;

internal interface IOVSPortManager
{
    EitherAsync<Error, Unit> SyncPorts(TypedPsObject<VirtualMachineInfo> vmInfo, VMPortChange change);
    EitherAsync<Error, Unit> SyncPorts(Guid vmId, VMPortChange change);


}