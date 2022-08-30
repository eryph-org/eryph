using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Messages.Resources.Machines.Commands;
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
                    await _bus.Publish(
                        OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId, ""));
                    return;
                }

                _imageRequestDispatcher.NewImageRequestTask(message.OperationId, message.TaskId, message.Command.Image);


            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Command '{nameof(PrepareVirtualMachineImageCommand)}' failed.");
                await _bus.Publish(OperationTaskStatusEvent.Failed(message.OperationId,
                    message.TaskId,
                    new ErrorData {ErrorMessage = ex.Message}));
            }
        }
    }


    [UsedImplicitly]
    public class RemoveVirtualDiskCommandHandler : IHandleMessages<OperationTask<RemoveVirtualDiskCommand>>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;
        public RemoveVirtualDiskCommandHandler(IBus bus, ILogger log)
        {
            _bus = bus;
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

                await _bus.Publish(OperationTaskStatusEvent.Completed(message.OperationId,
                    message.TaskId));
            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Command '{nameof(RemoveVirtualDiskCommand)}' failed.");
                await _bus.Publish(OperationTaskStatusEvent.Failed(message.OperationId,
                    message.TaskId,
                    new ErrorData { ErrorMessage = ex.Message }));
            }
        }
    }

}