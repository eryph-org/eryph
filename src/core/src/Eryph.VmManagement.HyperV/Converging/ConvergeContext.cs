using System;
using System.Threading.Tasks;
using Dbosoft.OVN.Windows;
using Eryph.ConfigModel.Catlets;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Resources.Machines;
using Eryph.VmManagement.Storage;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement.Converging;

public class ConvergeContext(
    VmHostAgentConfiguration vmHostAgentConfig,
    IPowershellEngine engine,
    IHyperVOvsPortManager portManager,
    Func<string, Task> reportProgress,
    CatletConfig config,
    Guid catletId,
    Guid vmId,
    bool secretDataHidden,
    VMStorageSettings storageSettings,
    MachineNetworkSettings[] networkSettings,
    VMHostMachineData hostInfo,
    Seq<UniqueGeneIdentifier> resolvedGenes,
    ILoggerFactory loggerFactory)
{
    public readonly Guid CatletId = catletId;
    public readonly CatletConfig Config = config;
    public readonly IPowershellEngine Engine = engine;
    public readonly VMHostMachineData HostInfo = hostInfo;
    public readonly ILoggerFactory LoggerFactory = loggerFactory;
    public readonly MachineNetworkSettings[] NetworkSettings = networkSettings;
    public readonly IHyperVOvsPortManager PortManager = portManager;
    public readonly Func<string, Task> ReportProgress = reportProgress;
    public readonly Seq<UniqueGeneIdentifier> ResolvedGenes = resolvedGenes;
    public readonly bool SecretDataHidden = secretDataHidden;
    public readonly VMStorageSettings StorageSettings = storageSettings;
    public readonly VmHostAgentConfiguration VmHostAgentConfig = vmHostAgentConfig;
    public readonly Guid VmId = vmId;


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
