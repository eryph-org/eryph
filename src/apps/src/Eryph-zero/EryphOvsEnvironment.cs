﻿using System.Runtime.InteropServices;
using Dbosoft.OVN;
using Dbosoft.OVN.Windows;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

public class EryphOvsEnvironment(
    IEryphOvsPathProvider runPathProvider,
    ILoggerFactory loggerFactory)
    : WindowsSystemEnvironment(loggerFactory)
{
    public override IFileSystem FileSystem => new EryphOvsFileSystem(runPathProvider);

    private sealed class EryphOvsFileSystem(
        IEryphOvsPathProvider runPathProvider)
        : DefaultFileSystem(OSPlatform.Windows)
    {
        protected override string FindBasePath(string pathRoot) =>
            !pathRoot.StartsWith("usr")
                ? base.FindBasePath(pathRoot)
                : runPathProvider.OvsRunPath;
    }
}
