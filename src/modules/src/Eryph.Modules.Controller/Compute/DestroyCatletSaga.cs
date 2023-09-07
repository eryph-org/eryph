using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.Commands;
using Eryph.Modules.Controller.DataServices;
using Eryph.Resources;
using JetBrains.Annotations;
using LanguageExt.UnsafeValueAccess;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute
{
    [UsedImplicitly]
    internal class DestroyCatletSaga : OperationTaskWorkflowSaga<DestroyCatletCommand, DestroyCatletSagaData>,
        IHandleMessages<OperationTaskStatusEvent<RemoveCatletVMCommand>>,
        IHandleMessages<OperationTaskStatusEvent<DestroyResourcesCommand>>
    {
        private readonly IVirtualMachineDataService _vmDataService;

        public DestroyCatletSaga(IWorkflow workflow,
            IVirtualMachineDataService vmDataService) : base(workflow)
        {
            _vmDataService = vmDataService;
        }

        public Task Handle(OperationTaskStatusEvent<RemoveCatletVMCommand> message)
        {
            return FailOrRun(message, async () =>
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

                await StartNewTask<DestroyResourcesCommand>(detachedDisks.Select(g => new Resource(ResourceType.VirtualDisk, g)).ToArray());

            });
        }

        protected override void CorrelateMessages(ICorrelationConfig<DestroyCatletSagaData> config)
        {
            base.CorrelateMessages(config);
            config.Correlate<OperationTaskStatusEvent<RemoveCatletVMCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
            config.Correlate<OperationTaskStatusEvent<DestroyResourcesCommand>>(m => m.InitiatingTaskId, d => d.SagaTaskId);
        }


        protected override async Task Initiated(DestroyCatletCommand message)
        {
            Data.MachineId = message.Resource.Id;
            var res = await _vmDataService.GetVM(Data.MachineId);
            var data = res.ValueUnsafe();

            if (data == null || data.VMId == Guid.Empty)
            {
                await Complete(Enumerable.Empty<Resource>(), Enumerable.Empty<Resource>());
                return;
            }

            await StartNewTask(new RemoveCatletVMCommand
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

        private Task DeleteCatletFromDb()
        {
            return _vmDataService.RemoveVM(Data.MachineId);
        }

        private async Task Complete(IEnumerable<Resource>? additionalDestroyed, IEnumerable<Resource>? detached)
        {
            additionalDestroyed ??= Array.Empty<Resource>();
            await DeleteCatletFromDb();

            await Complete(new DestroyResourcesResponse
            {
                DestroyedResources = additionalDestroyed.Concat(new[] { new Resource(ResourceType.Catlet, Data.MachineId) }).ToArray(),
                DetachedResources = detached == null ? Array.Empty<Resource>() : detached.ToArray()
            });
        }
    }
}