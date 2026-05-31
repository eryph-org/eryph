using System;
using System.Collections.Generic;
using Dbosoft.Rebus.Configuration;
using Eryph.Messages.Components;
using Eryph.Rebus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SimpleInjector;

namespace Eryph.ModuleCore.Components;

/// <summary>
/// Host-side helper that registers the bus transport for a split-runtime component: when
/// <c>componentMtls:enabled</c> is set it enrolls (blocking, retry-tolerant) and connects over
/// mTLS; otherwise it registers the plaintext transport (the default dev path, no regression).
/// Call from the host filter before the module configures its bus.
/// </summary>
public static class ComponentMtlsTransport
{
    public static void Register(
        Container container,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        ComponentType componentType)
    {
        var mtls = configuration.GetSection("componentMtls");
        if (!bool.TryParse(mtls["enabled"], out var enabled) || !enabled)
        {
            container.Register<IRebusTransportConfigurer, RabbitMqRebusTransportConfigurer>();
            return;
        }

        // The identity URL is pre-provisioned in config — it cannot arrive over the bus that the
        // certificate is needed to join.
        var endpointResolver = new EndpointResolver(new Dictionary<string, string>
        {
            ["identity"] = mtls["identityUrl"] ?? "",
        });
        var options = new ComponentEnrollmentClientOptions
        {
            EnrollmentSecret = mtls["enrollmentSecret"] ?? "",
        };

        var transport = ComponentEnrollment.EnsureEnrolledTransport(
            new ComponentIdentity(componentType, ""),
            endpointResolver,
            options,
            certificateDirectory: mtls["certificateDirectory"] ?? "",
            trustAnchorBundlePath: mtls["trustAnchorPath"] ?? "",
            renewalLeadTime: TimeSpan.FromDays(45),
            loggerFactory: loggerFactory);

        container.RegisterInstance<IRebusTransportConfigurer>(transport);
    }
}
