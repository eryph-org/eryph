using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net;
using System.Threading.Tasks;
using Haipa.Messages.Events;
using Haipa.Modules.VmHostAgent.Inventory;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Networking;
using LanguageExt;

namespace Haipa.Modules.VmHostAgent
{
    internal class VirtualMachineInventory
    {

        public Task<Either<PowershellFailure, MachineInfo>> InventorizeVM<T>(TypedPsObject<T> vm) where T: 
            IVirtualMachineCoreInfo, 
            IVMWithStateInfo, 
            IVMWithNetworkAdapterInfo<IVMNetworkAdapterWithConnection>
        {
            var info = new MachineInfo
            {
                MachineId = vm.Value.Id,
                Status = InventoryConverter.MapVmInfoStatusToVmStatus(vm.Value.State),
                Name = vm.Value.Name,
                NetworkAdapters = vm.Value.NetworkAdapters?.Map(a => new VirtualMachineNetworkAdapterInfo
                {
                    AdapterName = a.Name,
                    VirtualSwitchName = a.SwitchName
                }).ToArray(),
                Networks = VirtualNetworkQuery.GetNetworksByAdapters(vm.Value.Id, vm.Value.NetworkAdapters)
            };

            return Prelude.RightAsync<PowershellFailure, MachineInfo>(info).ToEither();
        }



    }
}