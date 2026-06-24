using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;
using Eryph.Modules.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <inheritdoc cref="IComponentEnrollmentService"/>
public sealed class ComponentEnrollmentService(
    IComponentCertificateAuthority certificateAuthority,
    IComponentEnrollmentPolicy policy,
    IEnumerable<IComponentBrokerProvisioner> brokerProvisioners,
    ILogger<ComponentEnrollmentService> logger)
    : IComponentEnrollmentService
{
    private const string ComponentUrnPrefix = ComponentBrokerIdentity.ComponentUrnPrefix;

    public async Task<ComponentEnrollmentResult> EnrollAsync(
        ComponentEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Fqdn))
            throw new ComponentEnrollmentException("The enrollment request must include the component FQDN.");
        if (!IsValidDnsName(request.Fqdn))
            throw new ComponentEnrollmentException("The component FQDN is not a valid DNS name.");

        // Validate and import everything the request carries BEFORE authorizing, because authorizing
        // redeems the one-time token. A recoverable client error (malformed key bytes, an invalid
        // server DNS name) must not consume a token that is still valid for a corrected retry.
        using var keys = ImportRequestKeys(request);

        // Authorize last: redeeming the one-time token is the final gate before issuance.
        if (!await policy.IsAuthorizedAsync(request, cancellationToken))
            throw new ComponentEnrollmentException("The component enrollment request was not authorized.");

        // The component id is derived server-side from the (authorized) type + FQDN, never taken
        // from the request, so an enrolling component cannot be issued a certificate for a
        // different identity than the one it authenticated as.
        var componentId = ComponentIdentity.DeriveComponentId(request.ComponentType, request.Fqdn);

        var result = Issue(componentId, request.Fqdn, keys);

        // Provision the component's broker user before returning: the component connects to the bus with
        // this certificate immediately after enrolling, and SASL EXTERNAL needs the matching user to
        // already exist. This is REQUIRED (unlike renewal's best-effort re-ensure): without the user the
        // component cannot connect, so a failure must fail the enrollment. The one-time token was already
        // redeemed, so log clearly that recovery is re-enrollment (a new token) before rethrowing.
        try
        {
            await EnsureBrokerUserAsync(componentId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Issued the certificate for {ComponentType} (component {ComponentId}) but failed to create "
                + "its broker user; enrollment cannot complete. The one-time token has been consumed, so "
                + "recovery requires a new enrollment file (re-enrollment).",
                request.ComponentType, componentId);
            throw;
        }

        logger.LogInformation(
            "Issued component certificate(s) for {ComponentType} (component {ComponentId}; server cert: {HasServer}).",
            request.ComponentType, componentId, result.ServerCertificate.Length > 0);
        return result;
    }

    public async Task<ComponentEnrollmentResult> RenewAsync(
        X509Certificate2 clientCertificate,
        ComponentEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clientCertificate);
        ArgumentNullException.ThrowIfNull(request);

        // Renewal authenticates with the component's CURRENT certificate (mTLS), not a one-time token:
        // the holder of a still-valid, CA-issued component certificate may obtain a fresh one. The
        // identity to renew is taken from the validated certificate (its URN SAN + CN), never from the
        // request, so a component can only renew its own identity.
        //
        // The renewal endpoint reads only the leaf (HttpContext.Connection.ClientCertificate) after the
        // handshake, so the issuing intermediate cannot ride along — the CA supplies its own intermediate
        // to build leaf -> intermediate -> root against the trusted root.
        var trust = certificateAuthority.GetTrustedCaCertificates();
        var issuing = certificateAuthority.GetIssuingCaCertificates();
        try
        {
            var trustBundle = new X509Certificate2Collection();
            foreach (var certificate in trust)
                trustBundle.Add(certificate);
            var intermediates = new X509Certificate2Collection();
            foreach (var certificate in issuing)
                intermediates.Add(certificate);
            if (!ComponentClientCertificateValidator.IsTrustedComponentClient(
                    clientCertificate, intermediates, trustBundle))
                throw new ComponentEnrollmentException(
                    "The presented certificate is not a trusted component certificate; cannot renew.");
        }
        finally
        {
            foreach (var certificate in trust)
                certificate.Dispose();
            foreach (var certificate in issuing)
                certificate.Dispose();
        }

        var (componentId, fqdn) = ReadComponentIdentity(clientCertificate);
        if (!IsValidDnsName(fqdn))
            throw new ComponentEnrollmentException("The certificate's host name is not a valid DNS name.");

        using var keys = ImportRequestKeys(request);
        var result = Issue(componentId, fqdn, keys);

        // Re-ensure the broker user, but best-effort: the certificate has already been renewed and the
        // user was created at enrollment, so a transient broker-management failure must NOT fail the
        // renewal (which would drop the freshly issued certificate and, on the endpoint, surface as a
        // 500). It is logged for the operator; the next renewal re-ensures it.
        try
        {
            await EnsureBrokerUserAsync(componentId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Renewed the certificate for component {ComponentId} but failed to re-ensure its broker "
                + "user; the existing user from enrollment remains in effect.",
                componentId);
        }

        logger.LogInformation(
            "Renewed component certificate(s) for component {ComponentId} ({Fqdn}; server cert: {HasServer}).",
            componentId, fqdn, result.ServerCertificate.Length > 0);
        return result;
    }

    private async Task EnsureBrokerUserAsync(Guid componentId, CancellationToken cancellationToken)
    {
        // Zero provisioners (eryph-zero's in-memory bus) makes this a no-op; the split runtime appends a
        // RabbitMQ provisioner.
        foreach (var provisioner in brokerProvisioners)
            await provisioner.EnsureComponentAsync(componentId, cancellationToken);
    }

    // Imports and validates the public keys + server DNS names a request carries. Held in a disposable
    // so both the enroll (token) and renew (certificate) paths free the key handles the same way.
    private static RequestKeys ImportRequestKeys(ComponentEnrollmentRequest request)
    {
        if (request.PublicKey is null || request.PublicKey.Length == 0)
            throw new ComponentEnrollmentException("The request must include the component public key.");

        var subjectKey = RSA.Create();
        try
        {
            subjectKey.ImportSubjectPublicKeyInfo(request.PublicKey, out _);
        }
        catch (CryptographicException ex)
        {
            subjectKey.Dispose();
            throw new ComponentEnrollmentException("The request public key is invalid.", ex);
        }

        // Cover every requested server name (default to the FQDN) — none is silently dropped, and
        // each must be a valid DNS name or the whole request is rejected.
        RSA? serverKey = null;
        IReadOnlyList<string>? serverDnsNames = null;
        if (request.ServerPublicKey is { Length: > 0 })
        {
            var dnsNames = request.ServerDnsNames is { Count: > 0 }
                ? request.ServerDnsNames.ToList()
                : [request.Fqdn];
            if (dnsNames.Any(string.IsNullOrWhiteSpace) || !dnsNames.All(IsValidDnsName))
            {
                subjectKey.Dispose();
                throw new ComponentEnrollmentException(
                    "A requested server DNS name is not a valid DNS name (wildcards and malformed names are rejected).");
            }

            serverKey = RSA.Create();
            try
            {
                serverKey.ImportSubjectPublicKeyInfo(request.ServerPublicKey, out _);
            }
            catch (CryptographicException ex)
            {
                serverKey.Dispose();
                subjectKey.Dispose();
                throw new ComponentEnrollmentException("The request server public key is invalid.", ex);
            }
            serverDnsNames = dnsNames;
        }

        return new RequestKeys(subjectKey, serverKey, serverDnsNames);
    }

    // Issues the client (and optional server) certificate plus the trust bundle for an established
    // identity. Shared by enroll and renew — the only difference is how the identity was proven.
    private ComponentEnrollmentResult Issue(Guid componentId, string fqdn, RequestKeys keys)
    {
        byte[] clientCertificate;
        List<byte[]> issuingChain;
        using (var issued = certificateAuthority.IssueComponentCertificate(
                   componentId.ToString(), fqdn, keys.SubjectKey))
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
            trustBundle = trustCertificates.Select(c => c.Export(X509ContentType.Cert)).ToList();
        }
        finally
        {
            foreach (var certificate in trustCertificates)
                certificate.Dispose();
        }

        byte[] serverCertificate = [];
        IReadOnlyList<byte[]> serverChain = [];
        if (keys.ServerDnsNames is not null && keys.ServerKey is not null)
        {
            using var issuedServer = certificateAuthority.IssueServerCertificate(keys.ServerDnsNames, keys.ServerKey);
            serverCertificate = issuedServer.Leaf.Export(X509ContentType.Cert);
            serverChain = issuedServer.IssuingChain
                .Select(certificate => certificate.Export(X509ContentType.Cert))
                .ToList();
        }

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

    // The component id is the URN SAN (urn:eryph:component:<id>) and the FQDN is the CN — the shape
    // IssueComponentCertificate writes. Both come from the validated certificate, so a renewing
    // component cannot claim another identity.
    private static (Guid ComponentId, string Fqdn) ReadComponentIdentity(X509Certificate2 certificate)
    {
        var fqdn = certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        if (string.IsNullOrWhiteSpace(fqdn))
            throw new ComponentEnrollmentException("The certificate has no common name (component host).");

        string? urn;
        try
        {
            urn = EnumerateSanUris(certificate)
                .FirstOrDefault(u => u.StartsWith(ComponentUrnPrefix, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is AsnContentException or CryptographicException or FormatException)
        {
            // A malformed subjectAltName must be rejected as an untrusted request, not bubble up as a 500.
            throw new ComponentEnrollmentException(
                "The certificate's subject alternative names could not be parsed.", ex);
        }

        if (urn is null || !Guid.TryParse(urn[ComponentUrnPrefix.Length..], out var componentId))
            throw new ComponentEnrollmentException(
                "The certificate does not carry a component identity URN; cannot renew.");

        return (componentId, fqdn);
    }

    // Reads the uniformResourceIdentifier ([6]) entries from the subjectAltName extension. The BCL's
    // X509SubjectAlternativeNameExtension exposes DNS names and IP addresses but not URIs, so the
    // extension's DER (a SEQUENCE OF GeneralName) is decoded directly to recover the component-id URN.
    private static IEnumerable<string> EnumerateSanUris(X509Certificate2 certificate)
    {
        var extension = certificate.Extensions
            .FirstOrDefault(e => e.Oid?.Value == "2.5.29.17");
        if (extension is null)
            yield break;

        var uriTag = new Asn1Tag(TagClass.ContextSpecific, 6);
        var sequence = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        while (sequence.HasData)
        {
            var tag = sequence.PeekTag();
            if (tag.TagClass == TagClass.ContextSpecific && tag.TagValue == 6)
                yield return sequence.ReadCharacterString(UniversalTagNumber.IA5String, uriTag);
            else
                sequence.ReadEncodedValue();
        }
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

    private sealed class RequestKeys(RSA subjectKey, RSA? serverKey, IReadOnlyList<string>? serverDnsNames)
        : IDisposable
    {
        public RSA SubjectKey { get; } = subjectKey;
        public RSA? ServerKey { get; } = serverKey;
        public IReadOnlyList<string>? ServerDnsNames { get; } = serverDnsNames;

        public void Dispose()
        {
            SubjectKey.Dispose();
            ServerKey?.Dispose();
        }
    }
}
