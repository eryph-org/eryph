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

namespace Eryph.Controller
{
    internal static class OvnHosting
    {
        /// <summary>
        /// Registers the OVN system environment for the controller's network handlers.
        /// The controller runtime is cross-platform; the concrete <see cref="ISystemEnvironment"/>
        /// is a host/platform concern (exactly as eryph-zero injects its Windows environment):
        /// the cross-platform <see cref="SystemEnvironment"/> (Dbosoft.OVN.Core) runs the OVS/OVN
        /// processes on Linux, while on Windows the <see cref="WindowsSystemEnvironment"/> is used.
        /// </summary>
        public static void UseOvn(this Container container)
        {
            var ovnSettings = new LocalOVSWithOVNSettings();
            container.RegisterInstance<IOVNSettings>(ovnSettings);
            container.RegisterInstance<IOvsSettings>(ovnSettings);

#if WINDOWS
            // The cross-platform Dbosoft.OVN.Core environment is Linux-only at runtime and
            // throws "Use the Dbosoft.OVN.Windows package" on Windows. Use the Windows
            // environment pointed at the OVN binaries unpacked by eryph-zero, mirroring
            // eryph-zero's EryphOvnEnvironment.
            var ovnProgramRoot = WindowsOvn.FindUnpackedOvnProgramRoot();
            container.RegisterSingleton<ISystemEnvironment>(
                () => new WindowsOvn.ControllerOvnEnvironment(
                    container.GetInstance<ILoggerFactory>(), ovnProgramRoot));
#else
            container.RegisterSingleton<ISystemEnvironment, SystemEnvironment>();
#endif
        }

#if WINDOWS
        private static class WindowsOvn
        {
            /// <summary>
            /// Locates the OVN program root unpacked by eryph-zero under
            /// <c>%ProgramData%\eryph\ovn\run_*</c> (the run dir containing <c>ovs-vsctl.exe</c>),
            /// returning the highest-numbered valid installation. The path is returned with a
            /// trailing separator because the base environment concatenates it with the "usr"
            /// segment via plain string concat.
            /// </summary>
            public static string FindUnpackedOvnProgramRoot()
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

            /// <summary>
            /// Windows OVN environment that redirects the program root to the unpacked OVN run
            /// dir, mirroring eryph-zero's <c>EryphOvnEnvironment</c>. The data root keeps the
            /// default Windows location.
            /// </summary>
            public sealed class ControllerOvnEnvironment(
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
