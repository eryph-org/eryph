using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Commands;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages;
using Eryph.Messages.Resources;
using Eryph.Messages.Resources.Catlets;
using Eryph.Rebus;
using Eryph.StateDb;
using Rebus.Bus;

namespace Eryph.Modules.Controller.Operations;

public class EryphRebusOperationMessaging(
    IBus bus,
    WorkflowOptions options,
    IOperationDispatcher operationDispatcher,
    IOperationTaskDispatcher taskDispatcher,
    IMessageEnricher messageEnricher,
    StateStoreContext dbContext)
    : RebusOperationMessaging(bus, operationDispatcher, taskDispatcher, messageEnricher, options)
{
    private readonly IBus _bus = bus;

    public override async Task DispatchTaskMessage(object command, IOperationTask task,
        IDictionary<string, string>? additionalHeaders = null)
    {
        var messageType = command.GetType();
        var outboundMessage = Activator.CreateInstance(
            typeof(OperationTaskSystemMessage<>).MakeGenericType(messageType),
            command, task.OperationId, task.InitiatingTaskId, task.Id, DateTimeOffset.UtcNow);
        var sendCommandAttribute = messageType.GetCustomAttribute<SendMessageToAttribute>();

        if (sendCommandAttribute == null)
            throw new InvalidOperationException(
                $"Invalid command type '{messageType}'. Type has to be decorated with SendMessageTo attribute.");

        var destination = sendCommandAttribute.Recipient switch
        {
            MessageRecipient.VMHostAgent => command switch
            {
                IVMCommand vmCommand =>
                    $"{QueueNames.VMHostAgent}.{await ResolveCatletAgent(vmCommand).ConfigureAwait(false)}",
                IHostAgentCommand agentCommand => $"{QueueNames.VMHostAgent}.{agentCommand.AgentName}",
                _ => throw new InvalidDataException(
                    $"Don't know how to route operation task command of type {messageType}"),
            },
            MessageRecipient.GenePoolAgent => command switch
            {
                IGenePoolAgentCommand agentCommand => $"{QueueNames.GenePool}.{agentCommand.AgentName}",
                _ => throw new InvalidDataException(
                    $"Don't know how to route operation task command of type {messageType}"),
            },
            MessageRecipient.Controllers => QueueNames.Controllers,
            _ => throw new ArgumentOutOfRangeException(),
        };

        // Record where the task was routed so a cancellation request can be delivered
        // directly to the host running the task instead of broadcasting to everyone.
        var taskModel = await dbContext.OperationTasks.FindAsync(task.Id).ConfigureAwait(false);
        if (taskModel is not null)
            taskModel.RoutedTo = destination;

        await _bus.Advanced.Routing.Send(destination, outboundMessage).ConfigureAwait(false);
    }

    private async Task<string> ResolveCatletAgent(IVMCommand vmCommand)
    {
        var machine = await dbContext.Catlets.FindAsync(vmCommand.CatletId).ConfigureAwait(false);
        if (machine == null)
            throw new InvalidOperationException($"Virtual catlet {vmCommand.CatletId} not found");
        if (string.IsNullOrEmpty(machine.AgentName))
            throw new InvalidOperationException($"Virtual catlet {vmCommand.CatletId} is not assigned to an agent.");

        return machine.AgentName;
    }
}
