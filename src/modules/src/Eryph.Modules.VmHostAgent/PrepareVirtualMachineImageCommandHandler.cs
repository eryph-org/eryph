using System;
using System.IO;
using System.Threading.Tasks;
using Eryph.Messages;
using Eryph.Messages.Operations;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Images.Commands;
using Eryph.Modules.VmHostAgent.Images;
using JetBrains.Annotations;
using LanguageExt;
using Microsoft.Extensions.Logging;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

namespace Eryph.Modules.VmHostAgent
{
    [UsedImplicitly]
    public class
        PrepareVirtualMachineImageCommandHandler : IHandleMessages<
            OperationTask<PrepareVirtualMachineImageCommand>>
    {
        private readonly IBus _bus;
        private readonly ILogger _log;
        private readonly IImageProvider _imageProvider;
        public PrepareVirtualMachineImageCommandHandler(IBus bus, ILogger log, IImageProvider imageProvider)
        {
            _bus = bus;
            _log = log;
            _imageProvider = imageProvider;
        }

        public async Task Handle(OperationTask<PrepareVirtualMachineImageCommand> message)
        {
            Task<Unit> ReportProgress(string progressMessage)
            {
                return ProgressMessage(message.OperationId, message.TaskId, progressMessage);
            }

            try
            {
                if (message.Command.Image == null)
                {
                    await _bus.Publish(
                        OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId));
                    return;
                }

                var hostSettings = HostSettingsBuilder.GetHostSettings();
                var imageRootPath = Path.Combine(hostSettings.DefaultVirtualHardDiskPath, "Images");

                await _imageProvider.ProvideImage(imageRootPath, message.Command.Image, ReportProgress).
                    ToAsync()
                    .MatchAsync(r => 
                        _bus.Publish(OperationTaskStatusEvent.Completed(message.OperationId, message.TaskId, r)), 
                        l=>
                        {
                            return _bus.Publish(OperationTaskStatusEvent.Failed(message.OperationId, message.TaskId,
                                l.Message));
                        });

            }
            catch (Exception ex)
            {
                _log.LogError(ex, $"Command '{nameof(PrepareVirtualMachineImageCommand)}' failed.");
                await _bus.Publish(OperationTaskStatusEvent.Failed(message.OperationId,
                    message.TaskId,
                    new ErrorData {ErrorMessage = ex.Message}));
            }
        }

        protected async Task<Unit> ProgressMessage(Guid operationId, Guid taskId, string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Publish(new OperationTaskProgressEvent
                {
                    Id = Guid.NewGuid(),
                    OperationId = operationId,
                    TaskId = taskId,
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }

            return Unit.Default;
        }
    }
}