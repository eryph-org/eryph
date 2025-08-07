using System;
using System.Collections.Generic;

namespace Eryph.Core
{
    public static class EryphConstants
    {
        public static readonly string AnyArchitecture = "any";
        public static readonly string DefaultArchitecture = "hyperv/amd64";

        public static readonly Guid DefaultTenantId = Guid.Parse("{C1813384-8ECB-4F17-B846-821EE515D19B}");
        public static readonly Guid DefaultProjectId = Guid.Parse("{4B4A3FCF-B5ED-4A9A-AB6E-03852752095E}");

        public static readonly string DefaultCatletName = "catlet";
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

        public static readonly int DefaultCatletCpuCount = 1;
        public static readonly int DefaultCatletMemoryMb = 1024;

        public static readonly string DefaultEastWestNetwork = "172.31.255.0/24";

        public static readonly string HgsGuardianName = "eryph-hgs-guardian";

        public static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);

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
            public static readonly string DynamicMemory = "dynamic_memory";
            public static readonly string Tpm = "tpm";
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

        public static class Limits
        {
            public static readonly int MaxCatletAncestors = 5;
            public static readonly int MaxGeneSetReferenceDepth = 5;
            public static readonly int MaxNetworkProviders = 100;
        }

        public static class Authorization
        {
            public static class Audiences
            {
                public static readonly string ComputeApi = "compute_api";
                public static readonly string IdentityApi = "identity_api";
            }

            public static class Scopes
            {
                public static readonly string ComputeRead = "compute:read";
                public static readonly string ComputeWrite = "compute:write";
                public static readonly string CatletsRead = "compute:catlets:read";
                public static readonly string CatletsWrite = "compute:catlets:write";
                public static readonly string CatletsControl = "compute:catlets:control";
                public static readonly string GenesRead = "compute:genes:read";
                public static readonly string GenesWrite = "compute:genes:write";
                public static readonly string ProjectsRead = "compute:projects:read";
                public static readonly string ProjectsWrite = "compute:projects:write";

                public static readonly string IdentityRead = "identity:read";
                public static readonly string IdentityWrite = "identity:write";
                public static readonly string IdentityClientsRead = "identity:clients:read";
                public static readonly string IdentityClientsWrite = "identity:clients:write";
            }

            public static readonly IReadOnlyList<Scope> AllScopes =
            [
                new(Scopes.ComputeRead, [Audiences.ComputeApi], "Grants read access to the compute API"),
                new(Scopes.ComputeWrite, [Audiences.ComputeApi], "Grants write access to the compute API"),
                new(Scopes.CatletsRead, [Audiences.ComputeApi], "Grants read access for catlets"),
                new(Scopes.CatletsWrite, [Audiences.ComputeApi], "Grants write access for catlets"),
                new(Scopes.CatletsControl, [Audiences.ComputeApi], "Grants control access (start, stop) for catlets"),
                new(Scopes.GenesRead, [Audiences.ComputeApi], "Grants read access for genes"),
                new(Scopes.GenesWrite, [Audiences.ComputeApi], "Grants write access for genes"),
                new(Scopes.ProjectsRead, [Audiences.ComputeApi], "Grants read access for projects"),
                new(Scopes.ProjectsWrite, [Audiences.ComputeApi], "Grants write access for projects"),

                new(Scopes.IdentityRead, [Audiences.IdentityApi], "Grants read access to the identity API"),
                new(Scopes.IdentityWrite, [Audiences.IdentityApi], "Grants write access to the identity API"),
                new(Scopes.IdentityClientsRead, [Audiences.IdentityApi], "Grants read access for identity clients"),
                new(Scopes.IdentityClientsWrite, [Audiences.IdentityApi], "Grants write access for identity clients"),
            ];

            public static readonly string SecuritySchemeId = "oauth2";

            public record Scope(string Name, IReadOnlyList<string> Resources, string Description);
        }
    }
}
