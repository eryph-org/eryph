using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Operations.Events;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Operations.Workflows
{
    [UsedImplicitly]
    internal class DestroyCatletSaga : OperationTaskWorkflowSaga<DestroyCatletCommand, DestroyCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<RemoveVirtualCatletCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IOperationTaskDispatcher _taskDispatcher;
        private readonly IVirtualMachineDataService _vmDataService;

        public DestroyCatletSaga(IBus bus, IOperationTaskDispatcher taskDispatcher,
            IVirtualMachineDataService vmDataService) : base(bus)
        {
            _taskDispatcher = taskDispatcher;
            _vmDataService = vmDataService;
        }

        public Task Handle(OperationTaskStatusEvent<RemoveVirtualCatletCommand> message)
        {
            return FailOrRun(message,async () =>
            {
                var detachedDisks = new List<Guid>();
                await _vmDataService.GetVM(Data.MachineId).IfSomeAsync(vm =>
                {
                    foreach (var virtualMachineDrive in vm.Drives
                                 .Where(virtualMachineDrive => virtualMachineDrive.AttachedDiskId != null 
                                                               || virtualMachineDrive.AttachedDiskId != Guid.Empty))
                    {
                        detachedDisks.Add(virtualMachineDrive.AttachedDiskId.GetValueOrDefault());
                    }
                });
                
                await _vmDataService.RemoveVM(Data.MachineId);

                await _taskDispatcher.StartNew<DestroyResourcesCommand>(Data.OperationId, 
                    detachedDisks.Select(g => new Resource(ResourceType.VirtualDisk, g)).ToArray());

            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<RemoveVirtualCatletCommand>>(m => m.OperationId, d => d.OperationId);
            config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(m => m.OperationId, d => d.OperationId);
        }


        protected override async Task Initiated(DestroyCatletCommand message)
        {
            Data.MachineId = message.Resource.Id;
            var res = await _vmDataService.GetVM(Data.MachineId);
            var data = res.ValueUnsafe();
            await _taskDispatcher.StartNew(Data.OperationId,new RemoveVirtualCatletCommand
            {
                CatletId = Data.MachineId,
                VMId = data.VMId
            });
        }

        public Task Handle(OperationTaskStatusEvent<DestroyResourcesCommand> message)
        {
            return FailOrRun<DestroyResourcesCommand, DestroyResourcesResponse>(message, 
                (response) => Complete(response.DestroyedResources, response.DetachedResources));
        }

        private Task Complete(IEnumerable<Resource>? additionalDestroyed, IEnumerable<Resource>? detached)
        {
            additionalDestroyed ??= Array.Empty<Resource>();
            return Complete(new DestroyResourcesResponse
            {
                DestroyedResources = additionalDestroyed.Concat(new[] { new Resource(ResourceType.Machine, Data.MachineId) }).ToArray(),
                DetachedResources = detached == null? Array.Empty<Resource>() : detached.ToArray()
            });
        }
    }
}