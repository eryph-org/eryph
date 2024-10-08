﻿using System.Runtime.InteropServices;
using Dbosoft.OVN;
using Microsoft.Extensions.Logging;

namespace Eryph.Runtime.Zero;

public class EryphOVSEnvironment : SystemEnvironment
{
    private readonly IEryphOvsPathProvider _runPathProvider;

    public EryphOVSEnvironment(IEryphOvsPathProvider runPathProvider, ILoggerFactory loggerFactory) : base(
        loggerFactory)
    {
        _runPathProvider = runPathProvider;
    }

    public override IFileSystem FileSystem => new EryphOVsFileSystem(_runPathProvider);


    private class EryphOVsFileSystem : DefaultFileSystem
    {
        private readonly IEryphOvsPathProvider _runPathProvider;

        public EryphOVsFileSystem(IEryphOvsPathProvider runPathProvider) : base(OSPlatform.Windows)
        {
            _runPathProvider = runPathProvider;
        }

        protected override string FindBasePath(string pathRoot)
        {
            if (!pathRoot.StartsWith("usr")) return base.FindBasePath(pathRoot);

            return _runPathProvider.OvsRunPath;

        }
    }
}