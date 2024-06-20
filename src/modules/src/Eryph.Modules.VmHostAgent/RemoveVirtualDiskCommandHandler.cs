using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core;
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

    private EitherAsync<Error, Unit> RemoveDisk(string path, string fileName) =>
        from hostSettings in hostSettingsProvider.GetHostSettings()
        from vmHostAgentConfig in vmHostAgentConfigurationManager.GetCurrentConfiguration(hostSettings)
        from storageSettings in DiskStorageSettings.FromVhdPath(
            powershellEngine, vmHostAgentConfig, Path.Combine(path, fileName))
        from _ in storageSettings.StorageIdentifier.IsSome && storageSettings.StorageNames.IsValid
            ? Try(() => DeleteFiles(path, fileName))
                .ToEither(ex => Error.New("Could not delete VM files", Error.New(ex)))
                .ToAsync()
            : unit
        select unit;

    private Unit DeleteFiles(string path, string fileName)
    {
        var filePath = Path.Combine(path, fileName);
        if(!fileSystem.File.Exists(filePath))
            return unit;

        fileSystem.File.Delete(filePath);

        if (fileSystem.Directory.Exists(path) && fileSystem.Directory.IsFolderTreeEmpty(path))
            fileSystem.Directory.Delete(path, true);

        return unit;
    }
}
