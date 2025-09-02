using Dbosoft.OVN.Windows;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Storage;
using Eryph.VmManagement.Tracing;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
internal class UpdateConfigDriveCommandHandler(
    IPowershellEngine engine,
    IHyperVOvsPortManager portManager,
    ITaskMessaging messaging,
    ILogger log,
    IHostInfoProvider hostInfoProvider,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager)
    : CatletConfigCommandHandler<UpdateCatletConfigDriveCommand, Unit>(engine, messaging, log)
{
    protected override EitherAsync<Error, Unit> HandleCommand(
        UpdateCatletConfigDriveCommand command) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from hostInfo in hostInfoProvider.GetHostInfoAsync().WriteTrace()
        let vmId = command.VmId
        from vmInfo in VmQueries.GetVmInfo(Engine, vmId)
        from metadata in EnsureMetadata(vmInfo, command.MetadataId).WriteTrace()
        from currentStorageSettings in VMStorageSettings.FromVM(vmHostAgentConfig, vmInfo).WriteTrace()
            .Bind(o => o.ToEither(Error.New("Could not find storage settings for VM.")).ToAsync())
        let fedConfig = command.Config
        from substitutedConfig in CatletConfigVariableSubstitutions.SubstituteVariables(fedConfig)
            .ToEither()
            .MapLeft(issues => Error.New("The substitution of variables failed.", Error.Many(issues.Map(i => i.ToError()))))
            .ToAsync()
        // We must redact the config after substituting the variables as the placeholder,
        // which is used to replace secret values, is not a valid variable value for some
        // variable types (e.g. boolean, number).
        let redactedConfig = command.SecretDataHidden
            ? CatletConfigRedactor.RedactSecrets(substitutedConfig)
            : substitutedConfig
        from vmInfoConverged in VirtualMachine.ConvergeConfigDrive(
                vmHostAgentConfig, hostInfo, Engine, portManager, ProgressMessage, vmInfo,
                redactedConfig, command.CatletId, command.SecretDataHidden, currentStorageSettings)
            .WriteTrace()
        select Unit.Default;
}
