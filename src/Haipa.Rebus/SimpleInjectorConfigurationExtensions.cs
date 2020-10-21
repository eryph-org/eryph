using System;
using System.Collections.Generic;
using System.Linq;
using Rebus.Bus;
using Rebus.Bus.Advanced;
using Rebus.Config;
using Rebus.Messages;
using Rebus.Pipeline;
using Rebus.Transport;
using SimpleInjector;

namespace Haipa.Rebus
{
    /// <summary>
    /// Configuration extensions for configuring a Rebus endpoint in your SimpleInjector container
    /// </summary>
    public static class SimpleInjectorConfigurationExtensions
    {
        /// <summary>
        /// Makes the necessary registrations in the container, registering the passed-in <paramref name="configurationCallback"/>
        /// as a configuration callback. The callback is invoked with a <see cref="RebusConfigurer"/> which must have
        /// its <see cref="RebusConfigurer.Start"/> method called at the end, returning the resulting <see cref="IBus"/> instance.
        /// The configuration callback is called the first time the bus is resolved, which may be done manually or simply by calling
        /// <see cref="StartBus"/>
        /// </summary>
        public static void ConfigureRebus(this Container container, Func<RebusConfigurer, IBus> configurationCallback)
        {
            if (container.GetCurrentRegistrations().Any(r => r.ServiceType == typeof(IBus)))
            {
                throw new InvalidOperationException("Cannot register IBus in the container because it has already been registered. If you want to host multiple Rebus instances in a single process, please use separate container instances for them.");
            }

            container.Register(() =>
            {
                if (container.IsVerifying) return new FakeSyncBus();

                return container.GetInstance<IBus>().Advanced.SyncBus;
            });

            container.Register(() =>
            {
                var currentMessageContext = MessageContext.Current;

                if (currentMessageContext != null)
                {
                    return currentMessageContext;
                }

                if (container.IsVerifying)
                {
                    return new FakeMessageContext();
                }

                throw new InvalidOperationException("Attempted to inject the current message context from MessageContext.Current, but it was null! Did you attempt to resolve IMessageContext from outside of a Rebus message handler?");
            });

            container.Register(() =>
            {
                var containerAdapter = new SimpleInjectorContainerAdapter(container);
                var rebusConfigurer = Configure.With(containerAdapter);
                return configurationCallback(rebusConfigurer);
            }, Lifestyle.Singleton);
        }

        /// <summary>
        /// After having configured the bus with <see cref="ConfigureRebus"/> the bus may be started by calling this method
        /// </summary>
        public static void StartBus(this Container container) => container.GetInstance<IBus>();

        class FakeSyncBus : ISyncBus
        {
            public void SendLocal(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Send(object commandMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Reply(object replyMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Defer(TimeSpan delay, object message, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void DeferLocal(TimeSpan delay, object message, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void SendLocal(object commandMessage, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Send(object commandMessage, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Reply(object replyMessage, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Defer(TimeSpan delay, object message, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void DeferLocal(TimeSpan delay, object message, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Subscribe<TEvent>()
            {
                throw new NotImplementedException();
            }

            public void Subscribe(Type eventType)
            {
                throw new NotImplementedException();
            }

            public void Unsubscribe<TEvent>()
            {
                throw new NotImplementedException();
            }

            public void Unsubscribe(Type eventType)
            {
                throw new NotImplementedException();
            }

            public void Publish(object eventMessage, IDictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }

            public void Publish(object eventMessage, Dictionary<string, string> optionalHeaders = null)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Fake implementation of <see cref="IMessageContext"/> that can be returned by SimpleInjector while verifying the configuration
        /// </summary>
        class FakeMessageContext : IMessageContext
        {
            public ITransactionContext TransactionContext { get; }
            public IncomingStepContext IncomingStepContext { get; }
            public TransportMessage TransportMessage { get; }
            public Message Message { get; }
            public Dictionary<string, string> Headers { get; }
        }
    }
}