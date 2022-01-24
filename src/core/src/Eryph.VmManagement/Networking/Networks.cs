using System.Collections.Generic;
using Eryph.VmManagement.Data;

namespace Eryph.VmManagement.Networking
{
    public static class Networks
    {
        public static string GenerateName(ref List<string> networkNames, IVMNetworkAdapterWithConnection adapter)
        {
            var name = adapter.Name;
            if (adapter.VlanSetting.AccessVlanId != 0)
                name = $"{name} VLAN {adapter.VlanSetting.AccessVlanId}";

            var counter = 1;

            while (networkNames.Contains(name))
            {
                counter++;
                name = $"{name} {counter}";
            }

            networkNames.Add(name);
            return name;
        }
    }
}