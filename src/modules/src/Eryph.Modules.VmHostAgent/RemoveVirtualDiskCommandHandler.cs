using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
public class RemoveVirtualDiskCommandHandler(
    ITaskMessaging messaging,
    ILogger log,
    IFileSystem fileSystem)
    : IHandleMessages<OperationTask<RemoveVirtualDiskCommand>>
{
    public async Task Handle(OperationTask<RemoveVirtualDiskCommand> message)
    {
        try
        {
            var fullPath = Path.Combine(message.Command.Path, message.Command.FileName);
            DeleteDisk(fullPath);

            await messaging.CompleteTask(message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Command {CommandName} failed.",
                nameof(RemoveVirtualDiskCommandHandler));
            await messaging.FailTask(message, ex.Message);
        }
    }

    private void DeleteDisk(string path)
    {
        if(!fileSystem.File.Exists(path))
            return;

        fileSystem.File.Delete(path);

        var directoryInfo = fileSystem.DirectoryInfo.New(path);
        if (!directoryInfo.Exists)
            return;

        // TODO should we actually delete the directory?
        if (directoryInfo.GetFileSystemInfos().Length == 0)
            directoryInfo.Delete();
    }
}
