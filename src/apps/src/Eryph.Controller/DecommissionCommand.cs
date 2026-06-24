using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Configuration;
using Rebus.Activation;
using Rebus.Config;

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
            var componentId = ParseComponentId(args);
            if (componentId is null)
            {
                Console.Error.WriteLine(
                    "usage: eryph-controller decommission --component-id <guid>\n"
                    + "   or: eryph-controller decommission --type <ComponentType> --fqdn <host>");
                return 2;
            }

            // The component's certificate directory holds this controller's enrolled client certificate,
            // which authenticates the one-way bus connection (SASL EXTERNAL). Read it the same way the
            // host does (componentMtls:certificateDirectory), via environment configuration.
            var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
            var certificateDirectory = configuration.GetSection("componentMtls")["certificateDirectory"];
            if (string.IsNullOrWhiteSpace(certificateDirectory))
            {
                Console.Error.WriteLine(
                    "componentMtls:certificateDirectory must be set so the command can present the "
                    + "controller's certificate to the bus.");
                return 2;
            }

            var store = new FileComponentCertificateStore(
                certificateDirectory, ComponentCertificateDefaults.RenewalLeadTime);
            var pfxPath = store.GetClientCertificatePfxPath();
            if (pfxPath is null)
            {
                Console.Error.WriteLine(
                    $"No enrolled client certificate found in '{certificateDirectory}'; run on an enrolled "
                    + "control node.");
                return 2;
            }

            // A one-way client over the same mutual-TLS transport the controller uses; RABBITMQ_CONNECTION
            // STRING is read by the configurer. The message is addressed explicitly to the controller queue.
            var transport = new RabbitMqRebusTransportConfigurer(pfxPath);
            using var activator = new BuiltinHandlerActivator();
            using var bus = Configure.With(activator)
                .Transport(t => transport.ConfigureAsOneWayClient(t))
                .Serialization(s => s.UseEryphSettings())
                .Start();

            await bus.Advanced.Routing.Send(
                QueueNames.Controllers, new DecommissionComponentCommand { ComponentId = componentId.Value });

            Console.WriteLine($"Sent decommission request for component {componentId.Value} to the controller.");
            return 0;
        }

        // Accepts either an explicit component id or a (type, fqdn) pair the id is derived from — the same
        // derivation the component itself uses, so an operator can decommission by the human-facing name.
        private static Guid? ParseComponentId(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if ((args.Length - 1) % 2 != 0)
                return null;
            for (var i = 1; i + 1 < args.Length; i += 2)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal))
                    return null;
                map[args[i][2..]] = args[i + 1];
            }

            if (map.TryGetValue("component-id", out var idText))
                return Guid.TryParse(idText, out var id) ? id : null;

            if (map.TryGetValue("type", out var typeText)
                && Enum.TryParse<ComponentType>(typeText, ignoreCase: true, out var type)
                && Enum.IsDefined(type)
                && map.TryGetValue("fqdn", out var fqdn)
                && !string.IsNullOrWhiteSpace(fqdn))
                return ComponentIdentity.DeriveComponentId(type, fqdn);

            return null;
        }
    }
}
