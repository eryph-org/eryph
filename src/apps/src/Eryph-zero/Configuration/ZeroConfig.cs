using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Eryph.Core;

namespace Eryph.Runtime.Zero.Configuration
{
    public static class ZeroConfig
    {
        public static string GetConfigPath()
        {
            return Config.GetConfigPath("zero");
        }

        public static string GetVmHostAgentConfigPath()
        {
            var privateConfigPath = GetPrivateConfigPath();
            var agentSettingsPath = Path.Combine(privateConfigPath, "agentsettings");

            return agentSettingsPath;
        }

        public static string GetClientConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var clientsConfigPath = Path.Combine(privateConfigPath, "clients");

            return clientsConfigPath;
        }

        public static string GetVMConfigPath()
        {
            var privateConfigPath = Path.Combine(GetConfigPath(), "private");
            var vmConfigPath = Path.Combine(privateConfigPath, "vms");

            return vmConfigPath;
        }

        public static string GetMetadataConfigPath()
        {
            var vmConfigPath = GetVMConfigPath();
            var metadataConfigPath = Path.Combine(vmConfigPath, "md");

            return metadataConfigPath;
        }

        public static string GetStorageConfigPath()
        {
            var vmConfigPath = GetVMConfigPath();
            var metadataConfigPath = Path.Combine(vmConfigPath, "storage");

            return metadataConfigPath;
        }


        public static string GetNetworksConfigPath()
        {
            var privateConfigPath = GetPrivateConfigPath();
            var networksConfigPath = Path.Combine(privateConfigPath, "networks");

            return networksConfigPath;
        }

        public static string GetPrivateConfigPath()
        {
            return Path.Combine(GetConfigPath(), "private");;
        }


        public static void EnsureConfiguration()
        {
            Config.EnsurePath(GetConfigPath());
            //Config.EnsurePath(GetPrivateConfigPath(), GetPrivateDirectorySecurity());
            Config.EnsurePath(GetClientConfigPath());
            Config.EnsurePath(GetVMConfigPath());
            Config.EnsurePath(GetMetadataConfigPath());
            Config.EnsurePath(GetStorageConfigPath());
            Config.EnsurePath(GetNetworksConfigPath());
            Config.EnsurePath(GetVmHostAgentConfigPath());
        }

        public static DirectorySecurity GetPrivateDirectorySecurity()
        {
            var directorySecurity = new DirectorySecurity();
            IdentityReference adminId = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            var adminAccess = new FileSystemAccessRule(
                adminId,
                FileSystemRights.FullControl,
                InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            IdentityReference systemId = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var systemAccess = new FileSystemAccessRule(
                systemId,
                FileSystemRights.FullControl,
                InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            directorySecurity.AddAccessRule(adminAccess);
            directorySecurity.AddAccessRule(systemAccess);
            // set the owner and the group to admins
            directorySecurity.SetAccessRuleProtection(true, true);

            return directorySecurity;
        }
    }
}