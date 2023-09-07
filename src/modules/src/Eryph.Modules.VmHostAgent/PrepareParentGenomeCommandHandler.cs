using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Genes.Commands;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Modules.VmHostAgent.Genetics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class
        PrepareParentGenomeCommandHandler : IHandleMessages<
            OperationTask<PrepareParentGenomeCommand>>
    {
        private readonly ITaskMessaging _messaging;
        private readonly ILogger _log;
        private readonly IGeneRequestDispatcher _imageRequestDispatcher;

        public PrepareParentGenomeCommandHandler(ITaskMessaging messaging, ILogger log,
            IGeneRequestDispatcher imageRequestDispatcher)
        {
            _messaging = messaging;
            _log = log;
            _imageRequestDispatcher = imageRequestDispatcher;
        }

        public async Task Handle(OperationTask<PrepareParentGenomeCommand> message)
        {

            try
            {
                if (message.Command.ParentName == null)
                {
                    await _messaging.CompleteTask(message, new PrepareParentGenomeResponse()
                    {
                        RequestedParent = "",
                        ResolvedParent = ""
                    });
                    return;
                }

                await _imageRequestDispatcher.NewGenomeRequestTask(message, message.Command.ParentName);


            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Command '{nameof(PrepareParentGenomeCommand)}' failed.");
                await _messaging.FailTask(message, ex.Message);
            }
        }
    }
}