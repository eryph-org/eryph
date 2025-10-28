using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations.Events;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets.Commands;
using Eryph.Messages.Resources.CatletSpecifications;
using Eryph.Messages.Resources.Commands;
using Eryph.ModuleCore;
using Eryph.Resources;
using JetBrains.Annotations;
using Rebus.Handlers;
using Rebus.Sagas;

namespace Eryph.Modules.Controller.Compute;

[UsedImplicitly]
internal class DestroyResourcesSaga(IWorkflow workflow) :
    OperationTaskWorkflowSaga<DestroyResourcesCommand, EryphSagaData<DestroyResourcesSagaData>>(workflow),
    IHandleMessages<OperationTaskStatusEvent<DestroyCatletCommand>>,
    IHandleMessages<OperationTaskStatusEvent<DestroyCatletSpecificationCommand>>,
    IHandleMessages<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>,
    IHandleMessages<OperationTaskStatusEvent<DestroyVirtualNetworksCommand>>
{
    protected override async Task Initiated(DestroyResourcesCommand message)
    {
        Data.Data.State = DestroyResourceState.Initiated;

        Data.Data.PendingCatlets = message.Resources
            .Where(r => r.Type == ResourceType.Catlet)
            .Select(r => r.Id)
            .ToHashSet();

        Data.Data.PendingCatletSpecifications = message.Resources
            .Where(r => r.Type == ResourceType.CatletSpecification)
            .Select(r => r.Id)
            .ToHashSet();

        Data.Data.PendingDisks = message.Resources
            .Where(r => r.Type == ResourceType.VirtualDisk)
            .Select(r => r.Id)
            .ToHashSet();

        Data.Data.PendingNetworks = message.Resources
            .Where(r => r.Type == ResourceType.VirtualNetwork)
            .Select(r => r.Id)
            .ToHashSet();

        await StartCatletTasks();
    }

    public Task Handle(OperationTaskStatusEvent<DestroyCatletCommand> message)
    {
        if (Data.Data.State >= DestroyResourceState.CatletsDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Collect(response);
            var removedCatlets = response.DestroyedResources.ToSeq()
                .Concat(response.DetachedResources.ToSeq())
                .Filter(r => r.Type is ResourceType.Catlet)
                .Map(r => r.Id);
            Data.Data.PendingCatlets = Data.Data.PendingCatlets
                .Except(removedCatlets)
                .ToHashSet();

            if (Data.Data.PendingCatlets.Count > 0)
                return;

            await StartCatletTasks();
        });
    }


    public Task Handle(OperationTaskStatusEvent<DestroyCatletSpecificationCommand> message)
    {
        if (Data.Data.State >= DestroyResourceState.OthersDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Collect(response);
            var removedCatlets = response.DestroyedResources.ToSeq()
                .Concat(response.DetachedResources.ToSeq())
                .Filter(r => r.Type is ResourceType.CatletSpecification)
                .Map(r => r.Id);
            Data.Data.PendingCatletSpecifications = Data.Data.PendingCatletSpecifications
                .Except(removedCatlets)
                .ToHashSet();

            if (Data.Data.PendingCatletSpecifications.Count > 0
                || Data.Data.PendingDisks.Count > 0
                || Data.Data.PendingNetworks.Count > 0)
                return;

            await CompleteWithResponse();
        });
    }


    public Task Handle(OperationTaskStatusEvent<DestroyVirtualDiskCommand> message)
    {
        if (Data.Data.State >= DestroyResourceState.OthersDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Collect(response);
            var removedDisks = response.DestroyedResources.ToSeq()
                .Concat(response.DetachedResources.ToSeq())
                .Filter(r => r.Type is ResourceType.VirtualDisk)
                .Map(r => r.Id);
            Data.Data.PendingDisks = Data.Data.PendingDisks
                .Except(removedDisks)
                .ToHashSet();

            if (Data.Data.PendingCatletSpecifications.Count > 0
                || Data.Data.PendingDisks.Count > 0
                || Data.Data.PendingNetworks.Count > 0)
                return;

            await CompleteWithResponse();
        });
    }

    public Task Handle(OperationTaskStatusEvent<DestroyVirtualNetworksCommand> message)
    {
        if (Data.Data.State >= DestroyResourceState.OthersDestroyed)
            return Task.CompletedTask;

        return FailOrRun(message, async (DestroyResourcesResponse response) =>
        {
            Collect(response);
            var removedNetworks = response.DestroyedResources.ToSeq()
                .Concat(response.DetachedResources.ToSeq())
                .Filter(r => r.Type is ResourceType.VirtualNetwork)
                .Map(r => r.Id);
            Data.Data.PendingNetworks = Data.Data.PendingNetworks
                .Except(removedNetworks)
                .ToHashSet();

            if (Data.Data.PendingNetworks.Count > 0)
            {
                await Fail($"Some networks were not removed: {string.Join(", ", Data.Data.PendingNetworks)}");
            }

            if (Data.Data.PendingCatletSpecifications.Count > 0
                || Data.Data.PendingDisks.Count > 0
                || Data.Data.PendingNetworks.Count > 0)
                return;

            await CompleteWithResponse();
        });
    }

    protected override void CorrelateMessages(
        ICorrelationConfig<EryphSagaData<DestroyResourcesSagaData>> config)
    {
        base.CorrelateMessages(config);
        config.Correlate<OperationTaskStatusEvent<DestroyCatletCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DestroyCatletSpecificationCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DestroyVirtualDiskCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
        config.Correlate<OperationTaskStatusEvent<DestroyVirtualNetworksCommand>>(
            m => m.InitiatingTaskId, d => d.SagaTaskId);
    }

    private async Task StartCatletTasks()
    {
        if (Data.Data.PendingCatlets.Count == 0)
        {
            await StartOtherTasks();
            return;
        }

        foreach (var catletId in Data.Data.PendingCatlets)
        {
            await StartNewTask(new DestroyCatletCommand { CatletId = catletId });
        }
    }

    private async Task StartOtherTasks()
    {
        Data.Data.State = DestroyResourceState.CatletsDestroyed;

        if (Data.Data.PendingCatletSpecifications.Count == 0
            && Data.Data.PendingDisks.Count == 0
            && Data.Data.PendingNetworks.Count == 0)
        {
            Data.Data.State = DestroyResourceState.OthersDestroyed;
            await CompleteWithResponse();
            return;
        }

        foreach (var catletSpecificationId in Data.Data.PendingCatletSpecifications)
        {
            await StartNewTask(new DestroyCatletSpecificationCommand
            {
                SpecificationId = catletSpecificationId
            });
        }

        foreach (var diskId in Data.Data.PendingDisks)
        {
            await StartNewTask(new DestroyVirtualDiskCommand
            {
                DiskId = diskId
            });
        }

        if (Data.Data.PendingNetworks.Count > 0)
        {
            await StartNewTask(new DestroyVirtualNetworksCommand
            {
                NetworkIds = Data.Data.PendingNetworks.ToArray()
            });
        }
    }

    private void Collect(DestroyResourcesResponse response)
    {
        Data.Data.DestroyedResources = Data.Data.DestroyedResources
            .Union(response.DestroyedResources.ToSeq())
            .ToHashSet();
        Data.Data.DetachedResources = Data.Data.DetachedResources
            .Union(response.DetachedResources.ToSeq())
            .ToHashSet();
    }

    private async Task CompleteWithResponse()
    {
        Data.Data.State = DestroyResourceState.OthersDestroyed;
        await Complete(new DestroyResourcesResponse
        {
            DestroyedResources = Data.Data.DestroyedResources.ToList(),
            DetachedResources = Data.Data.DetachedResources.ToList()
        });
    }
}
