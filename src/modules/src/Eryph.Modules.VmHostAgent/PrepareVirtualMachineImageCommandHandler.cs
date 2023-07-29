using System;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Modules.VmHostAgent.Images;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class
        PrepareVirtualMachineImageCommandHandler : IHandleMessages<
            OperationTask<PrepareVirtualMachineImageCommand>>
    {
        private readonly ITaskMessaging _messaging;
        private readonly ILogger _log;
        private readonly IImageRequestDispatcher _imageRequestDispatcher;
        public PrepareVirtualMachineImageCommandHandler(ITaskMessaging messaging, ILogger log, IImageRequestDispatcher imageRequestDispatcher)
        {
            _messaging = messaging;
            _log = log;
            _imageRequestDispatcher = imageRequestDispatcher;
        }

        public async Task Handle(OperationTask<PrepareVirtualMachineImageCommand> message)
        {

            try
            {
                if (message.Command.Image == null)
                {
                    await _messaging.CompleteTask(message, new PrepareVirtualMachineImageResponse()
                    {
                        RequestedImage = "",
                        ResolvedImage = ""
                    });
                    return;
                }

                _imageRequestDispatcher.NewImageRequestTask(message, message.Command.Image);


            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Command '{nameof(PrepareVirtualMachineImageCommand)}' failed.");
                await _messaging.FailTask(message, ex.Message);
            }
        }
    }
}