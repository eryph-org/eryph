using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Genes.Commands;
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
            if (message.Command.GeneName == null)
            {
                await _messaging.CompleteTask(message, new PrepareGeneResponse()
                { 
                    GeneType = message.Command.GeneType,
                    RequestedGene = "",
                    ResolvedGene = ""
                });
                return;
            }

            await _imageRequestDispatcher.NewGeneRequestTask(message, message.Command.GeneType, message.Command.GeneName);


        }
        catch (Exception ex)
        {
            _log.LogError(ex, $"Command '{nameof(PrepareGeneCommand)}' failed.");
            await _messaging.FailTask(message, ex.Message);
        }
    }
}