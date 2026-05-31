using System;
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

        var leaf = certificateAuthority.IssueComponentCertificate(
            componentId.ToString(), request.Fqdn, subjectKey);
        var trustBundle = certificateAuthority.GetTrustedCaCertificates()
            .Select(certificate => certificate.Export(X509ContentType.Cert))
            .ToList();

        logger.LogInformation(
            "Issued component certificate for {ComponentType} on {Fqdn} (component {ComponentId}).",
            request.ComponentType, request.Fqdn, componentId);

        return new ComponentEnrollmentResult
        {
            ComponentId = componentId,
            Certificate = leaf.Export(X509ContentType.Cert),
            CaTrustBundle = trustBundle,
        };
    }
}
