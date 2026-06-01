using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        // Everything needed to bootstrap is in the operator-delivered enrollment file: the identity
        // CA cert (trust anchor), the identity endpoint, and the one-time token. None of it can
        // arrive over the bus that the resulting certificate is needed to join.
        var enrollmentFilePath = mtls["enrollmentFile"]
            ?? throw new InvalidOperationException(
                "componentMtls:enrollmentFile must be set when componentMtls is enabled.");
        var enrollment = LoadEnrollmentFile(enrollmentFilePath);

        var certificateDirectory = mtls["certificateDirectory"] ?? "";
        Directory.CreateDirectory(certificateDirectory);

        // Materialise the trust anchor from the file so the enrollment HTTP client can pin it to
        // validate the identity TLS endpoint (it does not use the machine trust store).
        var trustAnchorPath = Path.Combine(certificateDirectory, "identity-ca.pem");
        File.WriteAllText(
            trustAnchorPath, PemEncoding.WriteString("CERTIFICATE", enrollment.IdentityCaCertificate));

        // The enrollment token is bound to a specific host FQDN; the most likely deployment mistake is
        // delivering a file to the wrong host, where the server would reject every attempt and the
        // component would retry forever. Surface that early using the file's (informational) bound FQDN
        // — the authoritative binding is still the signed token, checked server-side.
        var identity = new ComponentIdentity(componentType, "");
        if (!string.IsNullOrWhiteSpace(enrollment.Fqdn)
            && !string.Equals(enrollment.Fqdn, identity.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            loggerFactory.CreateLogger(typeof(ComponentMtlsTransport).FullName!).LogWarning(
                "The enrollment file is bound to host '{FileFqdn}' but this machine reports '{LocalFqdn}'. "
                + "Enrollment will be rejected until the file cut for this host is provided.",
                enrollment.Fqdn, identity.MachineName);
        }

        var endpointResolver = new EndpointResolver(new Dictionary<string, string>
        {
            ["identity"] = enrollment.IdentityEndpoint,
        });
        var options = new ComponentEnrollmentClientOptions { Token = enrollment.Token };

        var transport = ComponentEnrollment.EnsureEnrolledTransport(
            identity,
            endpointResolver,
            options,
            certificateDirectory: certificateDirectory,
            trustAnchorBundlePath: trustAnchorPath,
            renewalLeadTime: TimeSpan.FromDays(45),
            loggerFactory: loggerFactory);

        container.RegisterInstance<IRebusTransportConfigurer>(transport);
    }

    private static ComponentEnrollmentFile LoadEnrollmentFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"The enrollment file '{path}' was not found.");

        // Same JSON conventions the identity command writes it with (snake_case + string enums).
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Deserialize<ComponentEnrollmentFile>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"The enrollment file '{path}' is empty or invalid.");
    }
}
