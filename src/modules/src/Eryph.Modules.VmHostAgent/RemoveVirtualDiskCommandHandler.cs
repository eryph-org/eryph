using System;
using System.IO;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Catlets.Commands;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
public class RemoveVirtualDiskCommandHandler : IHandleMessages<OperationTask<RemoveVirtualDiskCommand>>
{
    private readonly ITaskMessaging _messaging;
    private readonly ILogger _log;
    public RemoveVirtualDiskCommandHandler(ITaskMessaging messaging, ILogger log)
    {
        _messaging = messaging;
        _log = log;
    }

    public async Task Handle(OperationTask<RemoveVirtualDiskCommand> message)
    {

        try
        {
            var fullPath = Path.Combine(message.Command.Path, message.Command.FileName);
            if(File.Exists(fullPath))
                File.Delete(fullPath);

            if (Directory.Exists(message.Command.Path))
            {
                if (Directory.GetFiles(message.Command.Path, "*", SearchOption.AllDirectories).Length == 0)
                {
                    Directory.Delete(message.Command.Path);
                }
            }

            await _messaging.CompleteTask(message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Command '{nameof(RemoveVirtualDiskCommand)}' failed.");
            await _messaging.FailTask(message, ex.Message);

        }
    }
}