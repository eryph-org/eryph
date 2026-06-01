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

        if (string.IsNullOrWhiteSpace(request.Fqdn))
            issues.Add(new ValidationIssue("$.fqdn", "The component FQDN is required."));
        else if (!ComponentEnrollmentService.IsValidDnsName(request.Fqdn))
            issues.Add(new ValidationIssue("$.fqdn", "The component FQDN is not a valid DNS name."));

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
            for (var i = 0; i < names.Count; i++)
            {
                if (!ComponentEnrollmentService.IsValidDnsName(names[i]))
                    issues.Add(new ValidationIssue(
                        $"$.server_dns_names[{i}]", $"'{names[i]}' is not a valid DNS name."));
            }
        }

        return issues.Count == 0
            ? Validation<ValidationIssue, EnrollComponentRequest>.Success(request)
            : Validation<ValidationIssue, EnrollComponentRequest>.Fail(issues.ToSeq());
    }

    // A SubjectPublicKeyInfo for RSA-4096 is ~550 bytes (~740 base64 chars); 4 KB is far above any key
    // we issue against yet bounds the decode/import work an anonymous caller can force with oversized input.
    private const int MaxPublicKeyBase64Length = 4 * 1024;

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
