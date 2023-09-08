using System;
using System.Threading.Tasks;
using Eryph.ConfigModel.Catlets;
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
        public readonly HostSettings HostSettings;
        public readonly Func<string, Task> ReportProgress;
        public readonly VMStorageSettings StorageSettings;
        public readonly VMHostMachineData HostInfo;
        public readonly CatletMetadata Metadata;
        public readonly MachineNetworkSettings[] NetworkSettings;

        public ConvergeContext(
            HostSettings hostSettings,
            IPowershellEngine engine,
            Func<string, Task> reportProgress,
            CatletConfig config,
            CatletMetadata metadata,
            VMStorageSettings storageSettings, 
            MachineNetworkSettings[] networkSettings,
            VMHostMachineData hostInfo)
        {
            HostSettings = hostSettings;
            Engine = engine;
            ReportProgress = reportProgress;
            Config = config;
            Metadata = metadata;
            StorageSettings = storageSettings;
            NetworkSettings = networkSettings;
            HostInfo = hostInfo;
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