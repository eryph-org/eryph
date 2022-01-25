﻿using System;
using System.Threading.Tasks;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement.Storage;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeContext
    {
        public readonly MachineConfig Config;
        public readonly IPowershellEngine Engine;
        public readonly HostSettings HostSettings;
        public readonly Func<string, Task> ReportProgress;
        public readonly VMStorageSettings StorageSettings;

        public ConvergeContext(
            HostSettings hostSettings,
            IPowershellEngine engine,
            Func<string, Task> reportProgress,
            MachineConfig config,
            VMStorageSettings storageSettings)
        {
            HostSettings = hostSettings;
            Engine = engine;
            ReportProgress = reportProgress;
            Config = config;
            StorageSettings = storageSettings;
        }
    }
}