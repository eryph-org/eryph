using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.StateDb.Model;
using Eryph.StateDb.MySql;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Controller
{
    /// <summary>
    /// Operator command that makes a component renew its certificate now, in-process, instead of waiting
    /// for the scheduled renewal window — useful to roll certificates onto a rotated CA or to verify
    /// renewal. It looks up the component's inbound queue in the registry and sends a
    /// <see cref="RenewComponentCertificateCommand"/> straight to it (over the bus, with this control
    /// node's certificate). Usage:
    /// <code>eryph-controller renew-component (--component-id &lt;guid&gt; | --type &lt;ComponentType&gt; --fqdn &lt;host&gt;)</code>
    /// </summary>
    internal static class RenewComponentCommand
    {
        public const string Verb = "renew-component";

        public static async Task<int> RunAsync(string[] args)
        {
            var componentId = OperatorCommandSupport.ParseComponentId(args);
            if (componentId is null)
            {
                Console.Error.WriteLine(
                    "usage: eryph-controller renew-component --component-id <guid>\n"
                    + "   or: eryph-controller renew-component --type <ComponentType> --fqdn <host>");
                return 2;
            }

            // The renew command is addressed to the component itself, so resolve its inbound queue from
            // the registry the controller maintains.
            var inboundQueue = await ResolveInboundQueueAsync(componentId.Value);
            if (inboundQueue is null)
            {
                Console.Error.WriteLine(
                    $"Component {componentId.Value} is not registered, so its inbound queue is unknown.");
                return 2;
            }

            var connection = OperatorCommandSupport.TryCreateBus();
            if (connection is null)
                return 2;

            var (bus, owner) = connection.Value;
            using (owner)
            {
                await bus.Advanced.Routing.Send(inboundQueue, new RenewComponentCertificateCommand());
            }

            Console.WriteLine(
                $"Sent renewal request for component {componentId.Value} to '{inboundQueue}'.");
            return 0;
        }

        private static async Task<string?> ResolveInboundQueueAsync(Guid componentId)
        {
            var connectionString = ControllerContainerExtensions.GetStateDbConnectionString();
            var builder = new DbContextOptionsBuilder<MySqlStateStoreContext>();
            new MySqlStateStoreContextConfigurer(connectionString).Configure(builder);
            await using var context = new MySqlStateStoreContext(builder.Options);

            return await context.Set<ComponentRegistration>()
                .Where(r => r.ComponentId == componentId)
                .Select(r => r.InboundQueue)
                .FirstOrDefaultAsync();
        }
    }
}
