using System;
using AutoMapper;
using Haipa.Messages;
using Haipa.Messages.Operations;
using Haipa.Rebus;
using Haipa.VmManagement;
using Newtonsoft.Json;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Logging;
using Rebus.Persistence.InMem;
using Rebus.Retry.Simple;
using Rebus.Routing.TypeBased;
using Rebus.Serialization.Json;
using SimpleInjector;

namespace Haipa.Agent
{
    class Program
    {
        static void Main(string[] args)
        {
            Mapper.Initialize(cfg => { });
            
            var container = new Container();
            container.Collection.Register(typeof(IHandleMessages<>), typeof(Program).Assembly);

            container.RegisterSingleton<IPowershellEngine, PowershellEngine>();
            container.RegisterSingleton<IVirtualMachineInfoProvider, VirtualMachineInfoProvider>();

            container.ConfigureRebus(configurer => configurer
                .Transport(t => t.UseRabbitMq("amqp://guest:guest@localhost", "agent.localhost"))
                .Routing(x => x.TypeBased()
                    .Map<OperationTaskProgressEvent>("Haipa.controller"))
                //.Routing(x => x.AddTransportMessageForwarder(message =>
                //{
                //    if (!message.Headers.ContainsKey("agent-hostname"))
                //        return Task.FromResult(ForwardAction.None);

                //    var hostname = message.Headers["agent-hostname"];
                //    return Task.FromResult(hostname == null || hostname == "localhost" ?
                //        ForwardAction.None :
                //        ForwardAction.ForwardTo($"agent.{hostname}"));
                //}))
                //.Subscriptions(x => x())
                .Options(x =>
                {
                    x.SimpleRetryStrategy();
                    x.SetNumberOfWorkers(5);
                })
                .Timeouts(x => x.StoreInMemory())
                .Serialization(x => x.UseNewtonsoftJson(new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }))
                .Logging(x => x.ColoredConsole(LogLevel.Debug)).Start());

            container.StartBus();

            container.GetInstance<IBus>().Advanced.Topics.Subscribe("topic.agent.localhost");

            Console.ReadLine();

        }
    }
}
