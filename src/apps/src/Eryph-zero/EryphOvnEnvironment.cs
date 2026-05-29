using System.IO;
using System.Runtime.InteropServices;
using Dbosoft.OVN;
using Dbosoft.OVN.Windows;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

public class EryphOvnEnvironment(
    IEryphOvnPathProvider runPathProvider,
    ILoggerFactory loggerFactory)
    : WindowsSystemEnvironment(loggerFactory)
{
    public override IFileSystem FileSystem => new EryphOvnFileSystem(runPathProvider);

    private sealed class EryphOvnFileSystem(
        IEryphOvnPathProvider runPathProvider)
        : DefaultFileSystem(OSPlatform.Windows)
    {
        // In Dbosoft.OVN 2.0 the single FindBasePath hook was split into
        // GetProgramRootPath (for `usr/*` paths — binaries and shared files)
        // and GetDataRootPath (for `etc/` and `var/` — DBs, logs, runtime).
        // We only redirect the program root to the unpacked OVN run dir;
        // the data root keeps the default Windows location.
        //
        // The base ResolveBasePath concatenates the returned root with the
        // path prefix (e.g. "usr") via plain string concat, so the result
        // must end with a directory separator — otherwise the unpacked dir
        // and the "usr" segment fuse into "run_N" + "usr" = "run_Nusr".
        protected override string GetProgramRootPath() =>
            runPathProvider.OvnRunPath + Path.DirectorySeparatorChar;
    }
}
