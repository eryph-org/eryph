using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <inheritdoc cref="IComponentEnrollmentService"/>
public sealed class ComponentEnrollmentService(
    IComponentCertificateAuthority certificateAuthority,
    IComponentEnrollmentPolicy policy,
    ILogger<ComponentEnrollmentService> logger)
    : IComponentEnrollmentService
{
    public ComponentEnrollmentResult Enroll(ComponentEnrollmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Fqdn))
            throw new ComponentEnrollmentException("The enrollment request must include the component FQDN.");
        if (request.PublicKey is null || request.PublicKey.Length == 0)
            throw new ComponentEnrollmentException("The enrollment request must include the component public key.");

        if (!policy.IsAuthorized(request))
            throw new ComponentEnrollmentException("The component enrollment request was not authorized.");

        // The component id is derived server-side from the (authorized) type + FQDN, never taken
        // from the request, so an enrolling component cannot be issued a certificate for a
        // different identity than the one it authenticated as.
        var componentId = ComponentIdentity.DeriveComponentId(request.ComponentType, request.Fqdn);

        using var subjectKey = RSA.Create();
        try
        {
            subjectKey.ImportSubjectPublicKeyInfo(request.PublicKey, out _);
        }
        catch (CryptographicException ex)
        {
            throw new ComponentEnrollmentException("The enrollment request public key is invalid.", ex);
        }

        var issued = certificateAuthority.IssueComponentCertificate(
            componentId.ToString(), request.Fqdn, subjectKey);
        var issuingChain = issued.IssuingChain
            .Select(certificate => certificate.Export(X509ContentType.Cert))
            .ToList();
        var trustBundle = certificateAuthority.GetTrustedCaCertificates()
            .Select(certificate => certificate.Export(X509ContentType.Cert))
            .ToList();

        // Also issue the component's server-TLS certificate when it supplied a server key, so it can
        // serve its own endpoint over TLS chaining to the same root (see IssueServerCertificate).
        byte[] serverCertificate = [];
        IReadOnlyList<byte[]> serverChain = [];
        if (request.ServerPublicKey is { Length: > 0 })
        {
            var dnsName = request.ServerDnsNames.FirstOrDefault() ?? request.Fqdn;
            if (string.IsNullOrWhiteSpace(dnsName))
                throw new ComponentEnrollmentException("A server certificate was requested without a DNS name.");

            using var serverKey = RSA.Create();
            try
            {
                serverKey.ImportSubjectPublicKeyInfo(request.ServerPublicKey, out _);
            }
            catch (CryptographicException ex)
            {
                throw new ComponentEnrollmentException("The enrollment request server public key is invalid.", ex);
            }

            var issuedServer = certificateAuthority.IssueServerCertificate(dnsName, serverKey);
            serverCertificate = issuedServer.Leaf.Export(X509ContentType.Cert);
            serverChain = issuedServer.IssuingChain
                .Select(certificate => certificate.Export(X509ContentType.Cert))
                .ToList();
        }

        logger.LogInformation(
            "Issued component certificate(s) for {ComponentType} on {Fqdn} (component {ComponentId}; server cert: {HasServer}).",
            request.ComponentType, request.Fqdn, componentId, serverCertificate.Length > 0);

        return new ComponentEnrollmentResult
        {
            ComponentId = componentId,
            Certificate = issued.Leaf.Export(X509ContentType.Cert),
            IssuingChain = issuingChain,
            ServerCertificate = serverCertificate,
            ServerIssuingChain = serverChain,
            CaTrustBundle = trustBundle,
        };
    }
}
