using System;
using System.IO;
using System.Linq;
using System.Management;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent
{
    public interface IHostSettingsProvider
    {
        EitherAsync<Error, HostSettings> GetHostSettings();
    }

    public class HostSettingsProvider : IHostSettingsProvider
    {
        public EitherAsync<Error, HostSettings> GetHostSettings() =>
            from queryResult in Try(() =>
            {
                var scope = new ManagementScope(@"\\.\root\virtualization\v2");
                var query = new ObjectQuery(
                    "select DefaultExternalDataRoot,DefaultVirtualHardDiskPath from Msvm_VirtualSystemManagementServiceSettingData");

                using var searcher = new ManagementObjectSearcher(scope, query);
                using var settingsCollection = searcher.Get();

                return settingsCollection.Cast<ManagementObject>().ToList();
            }).ToEitherAsync()
            from settings in queryResult.HeadOrNone().ToEitherAsync(Error.New("failed to query for hyper-v host settings"))
            from defaultDataPath in Optional(settings.GetPropertyValue("DefaultExternalDataRoot") as string)
                .ToEitherAsync(Error.New("Failed to lookup the Hyper-V setting DefaultExternalDataRoot"))
            from defaultVirtualHardDiskPath in Optional(
                    settings.GetPropertyValue("DefaultVirtualHardDiskPath") as string)
                .ToEitherAsync(Error.New("Failed to lookup the Hyper-V setting DefaultVirtualHardDiskPath"))
            select new HostSettings
            {
                DefaultDataPath = Path.Combine(defaultDataPath, "Eryph"),
                DefaultVirtualHardDiskPath = Path.Combine(defaultVirtualHardDiskPath, "Eryph"),
                DefaultNetwork = "nat"
            };
    }
}