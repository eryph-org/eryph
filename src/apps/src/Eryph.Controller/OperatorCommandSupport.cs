using System;
using System.Collections.Generic;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Configuration;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;

namespace Eryph.Controller;

/// <summary>
/// Shared plumbing for the controller's operator commands (decommission, renew): identifying a
/// target component and opening a one-way bus connection authenticated with this control node's own
/// enrolled certificate (mutual TLS / SASL EXTERNAL).
/// </summary>
internal static class OperatorCommandSupport
{
    /// <summary>
    /// Resolves the target component id from either an explicit <c>--component-id</c> or a
    /// <c>--type</c>/<c>--fqdn</c> pair (derived the same way the component derives its own id, so an
    /// operator can address it by the human-facing name). Returns null on malformed arguments.
    /// </summary>
    public static Guid? ParseComponentId(string[] args)
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
            && Enum.TryParse<ComponentType>(typeText, true, out var type)
            && Enum.IsDefined(type)
            && map.TryGetValue("fqdn", out var fqdn)
            && !string.IsNullOrWhiteSpace(fqdn))
            return ComponentIdentity.DeriveComponentId(type, fqdn);

        return null;
    }

    /// <summary>
    /// Opens a one-way bus client using this control node's enrolled certificate. Returns the bus and
    /// the activator that owns it (dispose the activator to stop the bus), or null after writing an
    /// actionable error if the certificate is unavailable. RABBITMQ_CONNECTIONSTRING is read by the
    /// transport configurer.
    /// </summary>
    public static (IBus Bus, IDisposable Owner)? TryCreateBus()
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var certificateDirectory = configuration.GetSection("componentMtls")["certificateDirectory"];
        if (string.IsNullOrWhiteSpace(certificateDirectory))
        {
            Console.Error.WriteLine(
                "componentMtls:certificateDirectory must be set so the command can present the "
                + "controller's certificate to the bus.");
            return null;
        }

        var store = new FileComponentCertificateStore(
            certificateDirectory, ComponentCertificateDefaults.RenewalLeadTime);
        var pfxPath = store.GetClientCertificatePfxPath();
        if (pfxPath is null)
        {
            Console.Error.WriteLine(
                $"No enrolled client certificate found in '{certificateDirectory}'; run on an enrolled "
                + "control node.");
            return null;
        }

        var transport = new RabbitMqRebusTransportConfigurer(pfxPath);
        var activator = new BuiltinHandlerActivator();
        var bus = Configure.With(activator)
            .Transport(t => transport.ConfigureAsOneWayClient(t))
            .Serialization(s => s.UseEryphSettings())
            .Start();
        return (bus, activator);
    }
}
