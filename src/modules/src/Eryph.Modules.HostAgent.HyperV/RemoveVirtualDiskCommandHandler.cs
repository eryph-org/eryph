using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
using Eryph.Core.VmAgent;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.VmManagement;
using Eryph.VmManagement.Storage;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
public class RemoveVirtualDiskCommandHandler(
    ITaskMessaging messaging,
    IPowershellEngine powershellEngine,
    ILogger log,
    IHostSettingsProvider hostSettingsProvider,
    IVmHostAgentConfigurationManager vmHostAgentConfigurationManager,
    IFileSystem fileSystem)
    : IHandleMessages<OperationTask<RemoveVirtualDiskCommand>>
{
    public Task Handle(OperationTask<RemoveVirtualDiskCommand> message)
    {
        return RemoveDisk(message.Command.Path, message.Command.FileName)
            .FailOrComplete(messaging, message);
    }

    private EitherAsync<Error, RemoveVirtualDiskCommandResponse> RemoveDisk(
        string path,
        string fileName) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        let vhdPath = Path.Combine(path, fileName)
        from pathExists in Try(() => fileSystem.File.Exists(vhdPath))
            .ToEitherAsync()
        from _2 in pathExists
            ? RemoveExistingDisk(vhdPath, vmHostAgentConfig)
            : RightAsync<Error, Unit>(unit)
        // We can take the timestamp after the operation as we are actively
        // deleting the disk.
        let timestamp = DateTimeOffset.UtcNow
        select new RemoveVirtualDiskCommandResponse
        {
            Timestamp = timestamp,
        };

    private EitherAsync<Error, Unit> RemoveExistingDisk(
        string vhdPath,
        VmHostAgentConfiguration vmHostAgentConfig) =>
        from storageSettings in DiskStorageSettings.FromVhdPath(
            powershellEngine, vmHostAgentConfig, vhdPath)
        from _1 in guard(storageSettings.Gene.IsNone,
            Error.New("The disk is part of the gene pool and cannot be deleted directly. Remove the gene instead."))
        from _2 in storageSettings.StorageIdentifier.IsSome && storageSettings.StorageNames.IsValid
            ? Try(() => DeleteFiles(vhdPath))
                .ToEither(ex => Error.New("Could not delete disk files.", Error.New(ex)))
                .ToAsync()
            : unit
        select unit;

    private Unit DeleteFiles(string vhdPath)
    {
        if(!fileSystem.File.Exists(vhdPath))
            return unit;

        fileSystem.File.Delete(vhdPath);

        var directoryPath = fileSystem.Path.GetDirectoryName(vhdPath);
        if (fileSystem.Directory.Exists(directoryPath) && fileSystem.Directory.IsFolderTreeEmpty(directoryPath))
            fileSystem.Directory.Delete(directoryPath, true);

        return unit;
    }
}
