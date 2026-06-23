using Dbosoft.OVN;
using Microsoft.Extensions.Logging;
using SimpleInjector;
#if WINDOWS
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dbosoft.OVN.Windows;
#endif

namespace Eryph.Network
{
    internal static class NetworkOvnHosting
    {
        /// <summary>
        /// Registers the OVN settings and system environment for the standalone network process. The
        /// process hosts the OVN northbound and southbound databases plus northd on the local pipe
        /// (<see cref="LocalOVSWithOVNSettings"/>); the remote SSL listeners that let the controller and
        /// agents reach the databases are opened separately by
        /// <see cref="OvnRemoteEndpointService"/>. The concrete <see cref="ISystemEnvironment"/> is a
        /// host/platform concern, exactly as in eryph-zero and the standalone controller.
        /// </summary>
        public static void UseOvn(this Container container)
        {
            var ovnSettings = new LocalOVSWithOVNSettings();
            container.RegisterInstance<IOVNSettings>(ovnSettings);
            container.RegisterInstance<IOvsSettings>(ovnSettings);

#if WINDOWS
            // The cross-platform Dbosoft.OVN.Core environment is Linux-only at runtime and throws on
            // Windows by design. Use the Windows environment pointed at the OVN binaries.
            var ovnProgramRoot = WindowsOvn.FindOvnProgramRoot();
            container.RegisterSingleton<ISystemEnvironment>(
                () => new WindowsOvn.NetworkOvnEnvironment(
                    container.GetInstance<ILoggerFactory>(), ovnProgramRoot));
#else
            container.RegisterSingleton<ISystemEnvironment, SystemEnvironment>();
#endif
        }

#if WINDOWS
        private static class WindowsOvn
        {
            /// <summary>
            /// Locates the OVN program root. An operator may pin it via <c>ERYPH_OVN_PROGRAM_ROOT</c>
            /// (the OVN install root, i.e. the directory that contains the <c>ovs-vsctl.exe</c> binary
            /// used as the marker below) — used where the binaries are not unpacked by eryph-zero (a
            /// dedicated network host, dev boxes). Otherwise it falls back to the package eryph-zero
            /// unpacks under <c>%ProgramData%\eryph\ovn\run_*</c>, picking the run directory that
            /// contains <c>ovs-vsctl.exe</c>.
            /// </summary>
            public static string FindOvnProgramRoot()
            {
                var configured = Environment.GetEnvironmentVariable("ERYPH_OVN_PROGRAM_ROOT");
                if (!string.IsNullOrWhiteSpace(configured))
                    return configured.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

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
                        $"No OVN binaries found. Set ERYPH_OVN_PROGRAM_ROOT to the OVN install root, or "
                        + $"run eryph-zero once to unpack them under '{ovnRoot}'.");

                return runDir.FullName + Path.DirectorySeparatorChar;
            }

            /// <summary>
            /// Windows OVN environment that redirects the program root to the resolved OVN install,
            /// mirroring eryph-zero's <c>EryphOvnEnvironment</c>. The data root keeps the default
            /// Windows location.
            /// </summary>
            public sealed class NetworkOvnEnvironment(
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
#endif
    }
}
