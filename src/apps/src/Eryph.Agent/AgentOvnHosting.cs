using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dbosoft.OVN;
using Dbosoft.OVN.Windows;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.Agent
{
    internal static class AgentOvnHosting
    {
        /// <summary>
        /// Registers the OVN/OVS system environment for the host-agent chassis. The agent is
        /// Windows-only (Hyper-V switch extension), so it always uses the
        /// <see cref="WindowsSystemEnvironment"/> pointed at the OVN binaries unpacked by
        /// eryph-zero, mirroring eryph-zero's own OVN environment.
        /// </summary>
        public static void UseOvn(this Container container)
        {
            var ovnSettings = new LocalOVSWithOVNSettings();
            container.RegisterInstance<IOVNSettings>(ovnSettings);
            container.RegisterInstance<IOvsSettings>(ovnSettings);

            var ovnProgramRoot = FindUnpackedOvnProgramRoot();
            container.RegisterSingleton<ISystemEnvironment>(
                () => new AgentOvnEnvironment(
                    container.GetInstance<ILoggerFactory>(), ovnProgramRoot));
        }

        private static string FindUnpackedOvnProgramRoot()
        {
            var ovnRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "eryph", "ovn");

            var root = new DirectoryInfo(ovnRoot);
            var runDir = root.Exists
                ? root.GetDirectories("run_*")
                    .Where(d => d.GetFiles("ovs-vsctl.exe", SearchOption.AllDirectories).Length > 0)
                    .OrderByDescending(d => int.TryParse(d.Name.Replace("run_", ""), out var n) ? n : 0)
                    .FirstOrDefault()
                : null;

            if (runDir is null)
                throw new InvalidOperationException(
                    $"No unpacked OVN package found under '{ovnRoot}'. Run eryph-zero once to install the OVN binaries.");

            return runDir.FullName + Path.DirectorySeparatorChar;
        }

        private sealed class AgentOvnEnvironment(
            ILoggerFactory loggerFactory,
            string ovnProgramRoot)
            : WindowsSystemEnvironment(loggerFactory)
        {
            public override IFileSystem FileSystem => new ProgramRootFileSystem(ovnProgramRoot);

            private sealed class ProgramRootFileSystem(string ovnProgramRoot)
                : DefaultFileSystem(OSPlatform.Windows)
            {
                protected override string GetProgramRootPath() => ovnProgramRoot;
            }
        }
    }
}
