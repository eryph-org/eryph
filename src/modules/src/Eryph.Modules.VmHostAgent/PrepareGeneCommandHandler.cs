using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Genes.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent;

[UsedImplicitly]
public class PrepareGeneCommandHandler : IHandleMessages<
    OperationTask<PrepareGeneCommand>>
{
    private readonly ITaskMessaging _messaging;
    private readonly ILogger _log;
    private readonly IGeneRequestDispatcher _imageRequestDispatcher;

    public PrepareGeneCommandHandler(ITaskMessaging messaging, ILogger log,
        IGeneRequestDispatcher imageRequestDispatcher)
    {
        _messaging = messaging;
        _log = log;
        _imageRequestDispatcher = imageRequestDispatcher;
    }

    public async Task Handle(OperationTask<PrepareGeneCommand> message)
    {
        try
        {
            await _imageRequestDispatcher.NewGeneRequestTask(
                message, message.Command.GeneIdentifier);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Command '{nameof(PrepareGeneCommand)}' failed.");
            await _messaging.FailTask(message, ex.Message);
        }
    }
}