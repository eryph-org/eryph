using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Disks;
using Eryph.VmManagement;
using Eryph.VmManagement.Inventory;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.HostAgent;

[UsedImplicitly]
internal class CreateVirtualDiskVMCommandHandler(
    ITaskMessaging messaging,
    IPowershellEngine engine,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IPlacementConfigProvider placementConfigProvider)
    : IHandleMessages<OperationTask<CreateVirtualDiskVMCommand>>
{
    public Task Handle(OperationTask<CreateVirtualDiskVMCommand> message) =>
        HandleCommand(message.Command).FailOrComplete(messaging, message);

    private EitherAsync<Error, CreateVirtualDiskVMCommandResponse> HandleCommand(
        CreateVirtualDiskVMCommand command) =>
        from _placement in ValidatePlacement(command.DataStore.Value, command.Environment.Value)
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let storageNames = new StorageNames()
        {
            ProjectName = command.ProjectName.Value,
            DataStoreName = command.DataStore.Value,
            EnvironmentName = command.Environment.Value
        }
        from basePath in storageNames.ResolveVolumeStorageBasePath(vmHostAgentConfig)
        let fileName = $"{command.Name.Value}.vhdx"
        let vhdPath = Path.Combine(basePath, command.StorageIdentifier.Value, fileName)
        let sizeBytes = command.Size * 1024L * 1024 * 1024
        let psCommand = PsCommandBuilder.Create()
            .AddCommand("New-VHD")
            .AddParameter("Path", vhdPath)
            .AddParameter("Dynamic")
            .AddParameter("SizeBytes", sizeBytes)
        from _ in engine.RunAsync(psCommand)
        from storageSettings in DiskStorageSettings.FromVhdPath(engine, vmHostAgentConfig, vhdPath)
        select new CreateVirtualDiskVMCommandResponse
        {
            DiskInfo = storageSettings.CreateDiskInfo(),
        };

    // The controller owns the datastore/environment vocabulary; reject a name it does not
    // distribute before resolving the local path. The default datastore/environment is
    // always allowed.
    private EitherAsync<Error, Unit> ValidatePlacement(string dataStore, string environment)
    {
        var placement = placementConfigProvider.Current;

        if (!PlacementConfigValidation.IsDataStoreAllowed(placement, dataStore))
            return LeftAsync<Error, Unit>(Error.New(
                $"The data store '{dataStore}' is not part of the controller placement configuration."));

        if (!PlacementConfigValidation.IsEnvironmentAllowed(placement, environment))
            return LeftAsync<Error, Unit>(Error.New(
                $"The environment '{environment}' is not part of the controller placement configuration."));

        return RightAsync<Error, Unit>(unit);
    }
}
