using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Dbosoft.Rebus.Operations.Commands;
using Dbosoft.Rebus.Operations.Workflow;
using Eryph.Messages.Resources.Catlets;
using Eryph.Messages.Resources;
using Eryph.Messages;
using Eryph.Rebus;
using Rebus.Bus;
using Eryph.StateDb;

namespace Eryph.Modules.Controller.Operations;

public class EryphRebusOperationMessaging : RebusOperationMessaging
{
    private readonly IBus _bus;
    private readonly StateStoreContext _dbContext;

    public EryphRebusOperationMessaging(IBus bus, WorkflowOptions options, IOperationDispatcher operationDispatcher,
        IOperationTaskDispatcher taskDispatcher, 
        IMessageEnricher messageEnricher, StateStoreContext dbContext) 
        : base(bus, operationDispatcher, taskDispatcher, messageEnricher, options)
    {
        _bus = bus;
        _dbContext = dbContext;
    }

    public override async Task DispatchTaskMessage(object command, IOperationTask task, IDictionary<string, string>? additionalHeaders = null)
    {
        var messageType = command.GetType();
        var outboundMessage = Activator.CreateInstance(
            typeof(OperationTaskSystemMessage<>).MakeGenericType(messageType),
            command, task.OperationId, task.InitiatingTaskId, task.Id, DateTimeOffset.UtcNow);
        var sendCommandAttribute = messageType.GetCustomAttribute<SendMessageToAttribute>();

        if (sendCommandAttribute == null)
            throw new InvalidOperationException(
                $"Invalid command type '{messageType}'. Type has to be decorated with SendMessageTo attribute.");

        switch (sendCommandAttribute.Recipient)
        {
            case MessageRecipient.VMHostAgent:
                {
                    switch (command)
                    {
                        case IVMCommand vmCommand:
                            {
                                var machine = await _dbContext.Catlets.FindAsync(vmCommand.CatletId)
                                .ConfigureAwait(false);

                                if (machine == null)
                                {
                                    throw new InvalidOperationException(
                                        $"Virtual catlet {vmCommand.CatletId} not found");

                                }

                                await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{machine.AgentName}",
                                        outboundMessage)
                                    .ConfigureAwait(false);

                                return;
                            }
                        case IHostAgentCommand agentCommand:
                            await _bus.Advanced.Routing.Send($"{QueueNames.VMHostAgent}.{agentCommand.AgentName}",
                                    outboundMessage)
                                .ConfigureAwait(false);

                            return;
                        default:
                            throw new InvalidDataException(
                                $"Don't know how to route operation task command of type {messageType}");
                    }
                }
            case MessageRecipient.GenePoolAgent:
            {
                switch (command)
                {

                    case IGenePoolAgentCommand agentCommand:
                        await _bus.Advanced.Routing.Send($"{QueueNames.GenePool}.{agentCommand.AgentName}",
                                outboundMessage)
                            .ConfigureAwait(false);

                        return;
                    default:
                        throw new InvalidDataException(
                            $"Don't know how to route operation task command of type {messageType}");
                }
            }
            case MessageRecipient.Controllers:
                await _bus.SendLocal(outboundMessage);
                return;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}