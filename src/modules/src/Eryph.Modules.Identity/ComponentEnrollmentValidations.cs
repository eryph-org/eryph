using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Dbosoft.Functional.Validations;
using Eryph.Modules.Identity.Endpoints.V1.Components;
using Eryph.Modules.Identity.Services;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.Modules.Identity;

/// <summary>
/// Validates the shape of a component enrollment request (required fields, DNS-name syntax, key
/// encoding). Authorization (token signature/expiry/host/type/one-time) is deliberately NOT done here
/// — that is the enrollment service's decision and is surfaced as an opaque 401, never as detailed
/// validation errors. Member paths are the request's JSON (snake_case) paths.
/// </summary>
public static class ComponentEnrollmentValidations
{
    /// <summary>Validates a token-based enrollment request (the enroll endpoint).</summary>
    public static Validation<ValidationIssue, EnrollComponentRequest> Validate(EnrollComponentRequest request)
    {
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(request.Token))
            issues.Add(new ValidationIssue("$.token", "The enrollment token is required."));
        else if (request.Token.Length > EnrollmentTokenCodec.MaxTokenLength)
            // Bound the token at the (anonymous) request layer with the same cap the codec enforces, so
            // an oversized body is rejected with a 400 before it is split/Base64-decoded downstream.
            issues.Add(new ValidationIssue(
                "$.token",
                $"The enrollment token must not exceed {EnrollmentTokenCodec.MaxTokenLength} characters."));

        // The FQDN is required on enroll (the token binds it; it also names the certificate). Renewal
        // does not validate it — the FQDN is taken from the presented certificate, not the request.
        if (string.IsNullOrWhiteSpace(request.Fqdn))
            issues.Add(new ValidationIssue("$.fqdn", "The component FQDN is required."));
        else if (!ComponentEnrollmentService.IsValidDnsName(request.Fqdn))
            issues.Add(new ValidationIssue("$.fqdn", "The component FQDN is not a valid DNS name."));

        AddShapeIssues(request, issues);
        return Result(request, issues);
    }

    /// <summary>
    /// Validates a renewal request (the renew endpoint). Renewal authenticates with the component's
    /// current certificate (mutual TLS), not a token, so the token is neither required nor validated, and
    /// the FQDN is taken from the certificate (not the request) — but the public-key encoding and the
    /// server-DNS-name caps are enforced identically, so the (anonymous) renew endpoint is as bounded as
    /// enroll against oversized/malformed input.
    /// </summary>
    public static Validation<ValidationIssue, EnrollComponentRequest> ValidateForRenewal(EnrollComponentRequest request)
    {
        var issues = new List<ValidationIssue>();
        AddShapeIssues(request, issues);
        return Result(request, issues);
    }

    // The request-shape checks common to enrollment and renewal: component type, public key encoding, and
    // the server certificate's key + DNS-name caps. The FQDN is intentionally NOT required/validated here
    // — enroll binds it via the token and renew derives it from the certificate.
    private static void AddShapeIssues(EnrollComponentRequest request, List<ValidationIssue> issues)
    {
        // Reject an out-of-range component type up front. The authoritative type binding is the signed
        // token, but an undefined enum value should never reach the issue path.
        if (!Enum.IsDefined(request.ComponentType))
            issues.Add(new ValidationIssue("$.component_type", "The component type is not a known value."));

        if (string.IsNullOrWhiteSpace(request.PublicKey))
            issues.Add(new ValidationIssue("$.public_key", "The component public key is required."));
        else if (!IsValidPublicKey(request.PublicKey))
            issues.Add(new ValidationIssue(
                "$.public_key", "The component public key is not a valid base64-encoded SubjectPublicKeyInfo."));

        if (!string.IsNullOrEmpty(request.ServerPublicKey))
        {
            if (!IsValidPublicKey(request.ServerPublicKey))
                issues.Add(new ValidationIssue(
                    "$.server_public_key", "The server public key is not a valid base64-encoded SubjectPublicKeyInfo."));

            var names = request.ServerDnsNames ?? [];
            // Bound the count on this anonymous endpoint before validating/issuing each name — a real
            // component lists a handful of SANs, so a large array is only an attempt to force work.
            if (names.Count > MaxServerDnsNames)
                issues.Add(new ValidationIssue(
                    "$.server_dns_names",
                    $"At most {MaxServerDnsNames} server DNS names may be requested."));
            else
                for (var i = 0; i < names.Count; i++)
                {
                    if (!ComponentEnrollmentService.IsValidDnsName(names[i]))
                        issues.Add(new ValidationIssue(
                            $"$.server_dns_names[{i}]", $"'{names[i]}' is not a valid DNS name."));
                }
        }
        else if (request.ServerDnsNames is { Count: > 0 })
        {
            // Server DNS names only apply to a server certificate; reject them when no server public key
            // was supplied rather than silently dropping them (and the certificate the caller expected).
            issues.Add(new ValidationIssue(
                "$.server_dns_names",
                "Server DNS names require a server public key ($.server_public_key)."));
        }
    }

    private static Validation<ValidationIssue, EnrollComponentRequest> Result(
        EnrollComponentRequest request, List<ValidationIssue> issues) =>
        issues.Count == 0
            ? Validation<ValidationIssue, EnrollComponentRequest>.Success(request)
            : Validation<ValidationIssue, EnrollComponentRequest>.Fail(issues.ToSeq());

    // A SubjectPublicKeyInfo for RSA-4096 is ~550 bytes (~740 base64 chars); 4 KB is far above any key
    // we issue against yet bounds the decode/import work an anonymous caller can force with oversized input.
    private const int MaxPublicKeyBase64Length = 4 * 1024;

    // A component requests its own FQDN plus a small number of aliases; cap the list so an anonymous
    // caller cannot force thousands of regex validations / SAN entries.
    private const int MaxServerDnsNames = 16;

    private static bool IsValidPublicKey(string base64)
    {
        // Reject oversized input before allocating/decoding it (FromBase64String + ImportSubjectPublicKeyInfo).
        if (base64.Length > MaxPublicKeyBase64Length)
            return false;
        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(bytes, out _);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }
}
