using System;
using System.Threading.Tasks;
using Eryph.Messages.Operations;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.VmHostAgent.Images;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class
        PrepareVirtualMachineImageCommandHandler : IHandleMessages<
            OperationTask<PrepareVirtualMachineImageCommand>>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;
        private readonly IImageRequestDispatcher _imageRequestDispatcher;
        public PrepareVirtualMachineImageCommandHandler(IBus bus, ILogger log, IImageRequestDispatcher imageRequestDispatcher)
        {
            _bus = bus;
            _log = log;
            _imageRequestDispatcher = imageRequestDispatcher;
        }

        public async Task Handle(OperationTask<PrepareVirtualMachineImageCommand> message)
        {

            try
            {
                if (message.Command.Image == null)
                {
                    await _bus.CompleteTask(message, new PrepareVirtualMachineImageResponse()
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
                await _bus.FailTask(message, ex.Message);
            }
        }
    }
}