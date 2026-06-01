using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    public async Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Fqdn))
            throw new ComponentEnrollmentException("The enrollment request must include the component FQDN.");
        if (!IsValidDnsName(request.Fqdn))
            throw new ComponentEnrollmentException("The component FQDN is not a valid DNS name.");
        if (request.PublicKey is null || request.PublicKey.Length == 0)
            throw new ComponentEnrollmentException("The enrollment request must include the component public key.");

        // Validate and import everything the request carries BEFORE authorizing, because authorizing
        // redeems the one-time token. A recoverable client error (malformed key bytes, an invalid
        // server DNS name) must not consume a token that is still valid for a corrected retry.
        using var subjectKey = RSA.Create();
        try
        {
            subjectKey.ImportSubjectPublicKeyInfo(request.PublicKey, out _);
        }
        catch (CryptographicException ex)
        {
            throw new ComponentEnrollmentException("The enrollment request public key is invalid.", ex);
        }

        // Cover every requested server name (default to the FQDN) — none is silently dropped, and
        // each must be a valid DNS name or the whole request is rejected.
        using RSA? serverKey = request.ServerPublicKey is { Length: > 0 } ? RSA.Create() : null;
        IReadOnlyList<string>? serverDnsNames = null;
        if (serverKey is not null)
        {
            var dnsNames = request.ServerDnsNames is { Count: > 0 }
                ? request.ServerDnsNames.ToList()
                : [request.Fqdn];
            if (dnsNames.Any(string.IsNullOrWhiteSpace))
                throw new ComponentEnrollmentException("A server certificate was requested with an empty DNS name.");
            if (!dnsNames.All(IsValidDnsName))
                throw new ComponentEnrollmentException(
                    "A requested server DNS name is not a valid DNS name (wildcards and malformed names are rejected).");
            try
            {
                serverKey.ImportSubjectPublicKeyInfo(request.ServerPublicKey!, out _);
            }
            catch (CryptographicException ex)
            {
                throw new ComponentEnrollmentException("The enrollment request server public key is invalid.", ex);
            }
            serverDnsNames = dnsNames;
        }

        // Authorize last: redeeming the one-time token is the final gate before issuance.
        if (!await policy.IsAuthorizedAsync(request, cancellationToken))
            throw new ComponentEnrollmentException("The component enrollment request was not authorized.");

        // The component id is derived server-side from the (authorized) type + FQDN, never taken
        // from the request, so an enrolling component cannot be issued a certificate for a
        // different identity than the one it authenticated as.
        var componentId = ComponentIdentity.DeriveComponentId(request.ComponentType, request.Fqdn);

        // The issued certificates and trust-bundle certificates are only needed to export their public
        // wire bytes; dispose their native handles afterwards so a long-running identity service does
        // not leak a handle per enrollment.
        byte[] clientCertificate;
        List<byte[]> issuingChain;
        using (var issued = certificateAuthority.IssueComponentCertificate(
                   componentId.ToString(), request.Fqdn, subjectKey))
        {
            clientCertificate = issued.Leaf.Export(X509ContentType.Cert);
            issuingChain = issued.IssuingChain
                .Select(certificate => certificate.Export(X509ContentType.Cert))
                .ToList();
        }

        var trustCertificates = certificateAuthority.GetTrustedCaCertificates();
        List<byte[]> trustBundle;
        try
        {
            trustBundle = trustCertificates
                .Select(certificate => certificate.Export(X509ContentType.Cert))
                .ToList();
        }
        finally
        {
            foreach (var certificate in trustCertificates)
                certificate.Dispose();
        }

        // Issue the component's server-TLS certificate when it supplied a server key, so it can serve
        // its own endpoint over TLS chaining to the same root (see IssueServerCertificate).
        byte[] serverCertificate = [];
        IReadOnlyList<byte[]> serverChain = [];
        if (serverDnsNames is not null)
        {
            using var issuedServer = certificateAuthority.IssueServerCertificate(serverDnsNames, serverKey!);
            serverCertificate = issuedServer.Leaf.Export(X509ContentType.Cert);
            serverChain = issuedServer.IssuingChain
                .Select(certificate => certificate.Export(X509ContentType.Cert))
                .ToList();
        }

        // Log only server-derived values (type, the derived component id) — the component id already
        // encodes the FQDN, and logging the raw caller-supplied FQDN would be a log-injection vector.
        logger.LogInformation(
            "Issued component certificate(s) for {ComponentType} (component {ComponentId}; server cert: {HasServer}).",
            request.ComponentType, componentId, serverCertificate.Length > 0);

        return new ComponentEnrollmentResult
        {
            ComponentId = componentId,
            Certificate = clientCertificate,
            IssuingChain = issuingChain,
            ServerCertificate = serverCertificate,
            ServerIssuingChain = serverChain,
            CaTrustBundle = trustBundle,
        };
    }

    // A certificate is issued for a caller-supplied name, so the name must be a syntactically valid
    // DNS name: labels of letters/digits/hyphens (no leading/trailing hyphen), dot-separated, total
    // <= 253. This rejects wildcards, whitespace, and otherwise malformed SAN entries. Shared with the
    // endpoint's request validation so both apply the same rule.
    internal static bool IsValidDnsName(string name) =>
        !string.IsNullOrEmpty(name)
        && name.Length <= 253
        && Regex.IsMatch(
            name,
            @"^(?!-)[A-Za-z0-9-]{1,63}(?<!-)(\.(?!-)[A-Za-z0-9-]{1,63}(?<!-))*$",
            RegexOptions.CultureInvariant);
}
