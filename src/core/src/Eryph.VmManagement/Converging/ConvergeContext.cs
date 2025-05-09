﻿using System;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeContext
    {
        public readonly CatletConfig Config;
        public readonly IPowershellEngine Engine;
        public readonly IHyperVOvsPortManager PortManager;
        public readonly VmHostAgentConfiguration VmHostAgentConfig;
        public readonly Func<string, Task> ReportProgress;
        public readonly VMStorageSettings StorageSettings;
        public readonly VMHostMachineData HostInfo;
        public readonly CatletMetadata Metadata;
        public readonly MachineNetworkSettings[] NetworkSettings;
        public readonly Seq<UniqueGeneIdentifier> ResolvedGenes;


        public ConvergeContext(
            VmHostAgentConfiguration vmHostAgentConfig,
            IPowershellEngine engine,
            IHyperVOvsPortManager portManager,
            Func<string, Task> reportProgress,
            CatletConfig config,
            CatletMetadata metadata,
            VMStorageSettings storageSettings, 
            MachineNetworkSettings[] networkSettings,
            VMHostMachineData hostInfo,
            Seq<UniqueGeneIdentifier> resolvedGenes)
        {
            VmHostAgentConfig = vmHostAgentConfig;
            Engine = engine;
            PortManager = portManager;
            ReportProgress = reportProgress;
            Config = config;
            Metadata = metadata;
            StorageSettings = storageSettings;
            NetworkSettings = networkSettings;
            HostInfo = hostInfo;
            ResolvedGenes = resolvedGenes;
        }

        public EitherAsync<Error, Unit> ReportProgressAsync(string message)
        {
            if (ReportProgress == null)
                return Unit.Default;

            return Prelude.TryAsync(async () =>
            {
                await ReportProgress(message);
                return Unit.Default;
            }).ToEither();
        }
    }
}