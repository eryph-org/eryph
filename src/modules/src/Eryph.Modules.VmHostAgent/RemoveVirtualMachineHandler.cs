using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.VmHostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
internal class RemoveVirtualMachineHandler(
    ITaskMessaging messaging,
    IPowershellEngine engine,
    IOVSPortManager ovsPortManager,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IFileSystem fileSystem)
    : CatletOperationHandlerBase<RemoveCatletVMCommand>(messaging, engine)
{
    private readonly IPowershellEngine _engine = engine;

    protected override EitherAsync<Error, Unit> HandleCommand(
        TypedPsObject<VirtualMachineInfo> vmInfo,
        RemoveCatletVMCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from storageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo)
        from stoppedVM in vmInfo.StopIfRunning(_engine).ToAsync().ToError()
        from uRemovePorts in ovsPortManager.SyncPorts(vmInfo, VMPortChange.Remove)
        from _ in stoppedVM.Remove(_engine).ToAsync().ToError()
        from __ in RemoveVMFiles(storageSettings)
        select unit;

    protected override PsCommandBuilder CreateGetVMCommand(Guid vmId) =>
        base.CreateGetVMCommand(vmId)
            .AddParameter("ErrorAction", "SilentlyContinue");

    private EitherAsync<Error, Unit> RemoveVMFiles(
        Option<VMStorageSettings> storageSettings) =>
        storageSettings
            .Filter(settings => !settings.Frozen)
            .Map(settings => settings.VMPath)
            .Map(vmPath => Try(() =>
                {
                    if (!fileSystem.Directory.Exists(vmPath))
                        return unit;

                    // The VM files are removed by Hyper-V. We need to remove the
                    // config drive which was created by us.
                    var configDrivePath = Path.Combine(vmPath, "configdrive.iso");
                    if (fileSystem.File.Exists(configDrivePath))
                        fileSystem.File.Delete(configDrivePath);

                    if (fileSystem.Directory.IsFolderTreeEmpty(vmPath))
                        fileSystem.Directory.Delete(vmPath, true);

                    return unit;
                })
                .ToEither(ex => Error.New("Could not delete VM files", Error.New(ex)))
                .ToAsync())
            .IfNone(unit);
}
