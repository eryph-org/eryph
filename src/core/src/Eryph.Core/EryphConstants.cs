using System;

namespace Eryph.Core
{
    public static class EryphConstants
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("{C1813384-8ECB-4F17-B846-821EE515D19B}");
        public static readonly Guid DefaultProjectId = Guid.Parse("{4B4A3FCF-B5ED-4A9A-AB6E-03852752095E}");

        public static readonly string DefaultProjectName = "default";
        public static readonly string DefaultEnvironmentName = "default";
        public static readonly string DefaultDataStoreName = "default";
        public static readonly string DefaultNetworkName = "default";
        public static readonly string DefaultProviderName = "default";
        public static readonly string DefaultSubnetName = "default";
        public static readonly string DefaultIpPoolName = "default";

        public static readonly string OverlaySwitchName = "eryph_overlay";
        public static readonly string SwitchExtensionName = "dbosoft Open vSwitch Extension";
        public static readonly string DriverModuleName = "DBO_OVSE";

        public static readonly string SystemClientId = "system-client";
        public static readonly Guid SuperAdminRole = Guid.Parse("{E5E83176-7543-4D01-BAEA-08A00EA064A6}");

        public static readonly int DefaultCatletMemoryMb = 1024;

        public static class BuildInRoles
        {
            public static readonly Guid Owner = Guid.Parse("{918D2C23-8E9A-41AE-8F0E-ADACA3BECBC4}");
            public static readonly Guid Contributor = Guid.Parse("{6C526814-2466-4BC1-92FD-1728C1152F3D}");
            public static readonly Guid Reader = Guid.Parse("{47982531-A6D2-41E4-AD6C-D5023DEB7710}");

        }

        public static class Capabilities
        {
            public static readonly string SecureBoot = "secure_boot";
            public static readonly string NestedVirtualization = "nested_virtualization";
        }

        public static class CapabilityDetails
        {
            public static readonly string Disabled = "disabled";
            public static readonly string Enabled = "enabled";
        }

        public static class SystemVariables
        {
            public static readonly string CatletId = "catletId";
            public static readonly string VmId = "vmId";
        }
    }
}
