using System;

namespace Eryph.VmManagement.Data
{
    public interface IVMNetworkAdapterWithConnection : IVMNetworkAdapterCore
    {
        string SwitchName { get; }
        Guid? SwitchId { get; }

        VMNetworkAdapterVlanSetting VlanSetting { get; }
    }
}