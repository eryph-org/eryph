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
            // Register a constructed instance, not the type: RabbitMqRebusTransportConfigurer has two
            // public constructors (plaintext and the mTLS certificate one), which SimpleInjector cannot
            // auto-wire. The parameterless constructor is the plaintext dev path.
            container.RegisterInstance<IRebusTransportConfigurer>(new RabbitMqRebusTransportConfigurer());
            return;
        }

        // Everything needed to bootstrap is in the operator-delivered enrollment file: the identity
        // CA cert (trust anchor), the identity endpoint, and the one-time token. None of it can
        // arrive over the bus that the resulting certificate is needed to join.
        var enrollmentFilePath = mtls["enrollmentFile"]
            ?? throw new InvalidOperationException(
                "componentMtls:enrollmentFile must be set when componentMtls is enabled.");
        var enrollment = LoadEnrollmentFile(enrollmentFilePath);

        // Private key material is written here and must land in a predictable, ACL-able location —
        // never the current working directory. Require it explicitly when mTLS is enabled, and create
        // it owner-only (0700 on Unix) so a permissive umask cannot expose key material on first run.
        var certificateDirectory = mtls["certificateDirectory"];
        if (string.IsNullOrWhiteSpace(certificateDirectory))
            throw new InvalidOperationException(
                "componentMtls:certificateDirectory must be set when componentMtls is enabled.");
        SecureDirectory.EnsureOwnerOnly(certificateDirectory);

        // Materialise the trust anchor from the file so the enrollment HTTP client can pin it to
        // validate the identity TLS endpoint (it does not use the machine trust store).
        var trustAnchorPath = Path.Combine(certificateDirectory, "identity-ca.pem");
        File.WriteAllText(
            trustAnchorPath, PemEncoding.WriteString("CERTIFICATE", enrollment.IdentityCaCertificate));

        // The enrollment token is bound to a specific component type and host FQDN; the most likely
        // deployment mistakes are delivering a file for the wrong component type or to the wrong host,
        // where the server rejects every attempt and the component retries forever. Surface those early
        // from the file's (informational) fields — the authoritative binding is the signed token,
        // checked server-side.
        var logger = loggerFactory.CreateLogger(typeof(ComponentMtlsTransport).FullName!);
        if (enrollment.ComponentType != componentType)
            logger.LogWarning(
                "The enrollment file is for component type '{FileType}' but this component is '{ActualType}'. "
                + "Enrollment will be rejected until the file for this component type is provided.",
                enrollment.ComponentType, componentType);

        var identity = new ComponentIdentity(componentType, "");
        if (!string.IsNullOrWhiteSpace(enrollment.Fqdn)
            && !string.Equals(enrollment.Fqdn, identity.MachineName, StringComparison.OrdinalIgnoreCase))
            logger.LogWarning(
                "The enrollment file is bound to host '{FileFqdn}' but this machine reports '{LocalFqdn}'. "
                + "Enrollment will be rejected until the file cut for this host is provided.",
                enrollment.Fqdn, identity.MachineName);

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

        // The same enrolled certificate material is what dials other components over mTLS (e.g. the
        // compute API's EGS remote-channel data plane dialing a host agent). Expose the store so those
        // consumers can present the client certificate and validate peers against the enrolled CA — the
        // single trust anchor, no divergent trust path.
        container.RegisterInstance<IComponentCertificateStore>(
            new FileComponentCertificateStore(certificateDirectory, TimeSpan.FromDays(45)));
    }

    private static ComponentEnrollmentFile LoadEnrollmentFile(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"The enrollment file '{path}' was not found.");

        // Same JSON conventions the identity command writes it with (snake_case + string enums).
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        options.Converters.Add(new JsonStringEnumConverter());
        var enrollment = JsonSerializer.Deserialize<ComponentEnrollmentFile>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException($"The enrollment file '{path}' is empty or invalid.");

        // Validate the fields we depend on so a truncated/tampered file fails with an actionable message
        // here rather than as a cryptic CryptographicException (empty PEM) or a TLS error far downstream.
        if (enrollment.IdentityCaCertificate is not { Length: > 0 })
            throw new InvalidOperationException(
                $"The enrollment file '{path}' has no identity CA certificate.");
        if (string.IsNullOrWhiteSpace(enrollment.IdentityEndpoint))
            throw new InvalidOperationException(
                $"The enrollment file '{path}' has no identity endpoint.");
        if (string.IsNullOrWhiteSpace(enrollment.Token))
            throw new InvalidOperationException(
                $"The enrollment file '{path}' has no enrollment token.");

        return enrollment;
    }
}
