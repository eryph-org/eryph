using System;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.Rebus;

namespace Eryph.Controller
{
    /// <summary>
    /// Operator command that permanently decommissions a component: it sends a
    /// <see cref="DecommissionComponentCommand"/> to the running controller, which deletes the
    /// component's broker user (the immediate, hard bus cutoff) and removes its registration. Run it on
    /// the control node — it connects to the bus with this controller's own enrolled certificate. Usage:
    /// <code>eryph-controller decommission (--component-id &lt;guid&gt; | --type &lt;ComponentType&gt; --fqdn &lt;host&gt;)</code>
    /// </summary>
    internal static class DecommissionCommand
    {
        public const string Verb = "decommission";

        public static async Task<int> RunAsync(string[] args)
        {
            var componentId = OperatorCommandSupport.ParseComponentId(args);
            if (componentId is null)
            {
                Console.Error.WriteLine(
                    "usage: eryph-controller decommission --component-id <guid>\n"
                    + "   or: eryph-controller decommission --type <ComponentType> --fqdn <host>");
                return 2;
            }

            var connection = OperatorCommandSupport.TryCreateBus();
            if (connection is null)
                return 2;

            var (bus, owner) = connection.Value;
            using (owner)
            {
                await bus.Advanced.Routing.Send(
                    QueueNames.Controllers, new DecommissionComponentCommand { ComponentId = componentId.Value });
            }

            Console.WriteLine($"Sent decommission request for component {componentId.Value} to the controller.");
            return 0;
        }
    }
}
