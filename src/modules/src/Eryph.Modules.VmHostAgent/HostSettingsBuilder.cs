using System;
using System.IO;
using System.Management;

namespace Eryph.Modules.VmHostAgent
{
    public class HostSettingsBuilder
    {
        public static HostSettings GetHostSettings()
        {
            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var query = new ObjectQuery(
                "select DefaultExternalDataRoot,DefaultVirtualHardDiskPath from Msvm_VirtualSystemManagementServiceSettingData");


            var searcher = new ManagementObjectSearcher(scope, query);
            var settingsCollection = searcher.Get();

            foreach (var hostSettings in settingsCollection)
                return new HostSettings
                {
                    DefaultVirtualHardDiskPath =
                        Path.Combine(hostSettings.GetPropertyValue("DefaultVirtualHardDiskPath")?.ToString(), "Eryph"),
                    DefaultDataPath = Path.Combine(hostSettings.GetPropertyValue("DefaultExternalDataRoot")?.ToString(),
                        "Eryph"),
                    DefaultNetwork = "nat"
                };

            throw new Exception("failed to query for hyper-v host settings");
        }
    }
}