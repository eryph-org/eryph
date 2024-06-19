using System;
using System.IO;
using System.IO.Abstractions;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;
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
            DeleteDisk(message.Command.Path, message.Command.FileName);

            await messaging.CompleteTask(message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Command {CommandName} failed.",
                nameof(RemoveVirtualDiskCommandHandler));
            await messaging.FailTask(message, ex.Message);
        }
    }

    private void DeleteDisk(string path, string fileName)
    {
        var filePath = Path.Combine(path, fileName);
        if(!fileSystem.File.Exists(filePath))
            return;

        fileSystem.File.Delete(filePath);

        var directoryInfo = fileSystem.DirectoryInfo.New(path);
        if (directoryInfo.Exists && directoryInfo.GetFileSystemInfos().Length == 0)
            directoryInfo.Delete();
    }
}
