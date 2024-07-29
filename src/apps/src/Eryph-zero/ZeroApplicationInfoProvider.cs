using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;

namespace Eryph.Runtime.Zero;

internal class ZeroApplicationInfoProvider : IApplicationInfoProvider
{
    public ZeroApplicationInfoProvider()
    {
        Name = "eryph-zero";

        var entryAssembly = Assembly.GetEntryAssembly()!;
        var fileVersionInfo = FileVersionInfo.GetVersionInfo(entryAssembly.Location);
        ProductVersion = fileVersionInfo.ProductVersion ?? "unknown";

        // ApplicationId is truncated to 24 characters for compatibility with AutoRest
        ApplicationId = $"zero-{ProductVersion}"[..24];
    }

    public string Name { get; }

    public string ProductVersion { get; }

    public string ApplicationId { get; set; }
}
