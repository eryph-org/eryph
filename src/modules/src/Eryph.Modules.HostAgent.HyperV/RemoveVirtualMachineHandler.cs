using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Modules.HostAgent.Networks;
using Eryph.Modules.HostAgent.Networks.OVS;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Full;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;
using SimpleInjector;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

using static VirtualMachineUtils<AgentRuntime>;
using static OvsPortCommands<AgentRuntime>;

[UsedImplicitly]
internal class RemoveVirtualMachineHandler(
    ITaskMessaging messaging,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    Scope serviceScope,
    IFileSystem fileSystem)
    : IHandleMessages<OperationTask<RemoveCatletVMCommand>>
{
    public async Task Handle(OperationTask<RemoveCatletVMCommand> message)
    {
        var result = await HandleCommand(message.Command)
            .Run(AgentRuntime.New(serviceScope));

        await result.FailOrComplete(messaging, message);
    }

    private Aff<AgentRuntime, Unit> HandleCommand(
        RemoveCatletVMCommand command) =>
        from optionalVmInfo in getOptionalVmInfo(command.VMId)
        from _ in optionalVmInfo.Map(RemoveVm).Sequence()
        select unit;

    private Aff<AgentRuntime, Unit> RemoveVm(
        TypedPsObject<VirtualMachineInfo> vmInfo) =>
        from hostSettings in hostSettingsProvider.GetHostSettings().ToAff()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings).ToAff()
        from storageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).ToAff()
        from _1 in timeout(EryphConstants.OperationTimeout, stopVm(vmInfo))
        from _2 in syncOvsPorts(vmInfo, VMPortChange.Remove)
        from _3 in removeVm(vmInfo)
        from _4 in RemoveVmFiles(storageSettings)
        select unit;

    private Eff<Unit> RemoveVmFiles(
        Option<VMStorageSettings> storageSettings) =>
        from _ in storageSettings
            .Filter(settings => !settings.Frozen)
            .Map(settings => RemoveVmFiles(settings.VMPath))
            .Sequence()
        select unit;

    private Eff<Unit> RemoveVmFiles(string vmPath) =>
        Eff(() =>
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
        .MapFail(e => Error.New("Could not delete VM files.", e));
}
