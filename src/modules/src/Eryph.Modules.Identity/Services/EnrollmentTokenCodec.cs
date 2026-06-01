using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eryph.Messages.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>The verified contents of an enrollment token.</summary>
public sealed record EnrollmentTokenContent(
    string Jti, ComponentType ComponentType, string Fqdn, DateTimeOffset ExpiresAt);

/// <summary>
/// The enrollment-token format: a payload (<c>jti</c>, bound component type, bound host FQDN, expiry)
/// signed by the component CA's root key and verified with the root certificate, so the token is
/// self-validating — nothing about it is secret. Issuing needs only the CA; reading verifies the
/// signature and decodes the payload. The expiry, host-binding and one-time decisions are the
/// redeemer's, not the codec's.
/// </summary>
/// <remarks>
/// The token is signed with the CA <i>root</i> key (the same key that signs the intermediates). This
/// is accepted because the signed payload is entirely server-generated (issuing is not an oracle for
/// caller-supplied bytes), so it is not a cross-protocol signing oracle. A dedicated
/// enrollment-signing key would be cleaner and is a possible follow-up.
/// </remarks>
public static class EnrollmentTokenCodec
{
    public static string Issue(
        IComponentCertificateAuthority certificateAuthority,
        ComponentType componentType,
        string fqdn,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(fqdn))
            throw new ArgumentException("The bound host FQDN must be provided.", nameof(fqdn));

        var payload = new TokenPayload
        {
            Jti = Guid.NewGuid().ToString("N"),
            Type = componentType,
            // DNS is case-insensitive; normalise so the redeem-time comparison is stable.
            Fqdn = fqdn.ToLowerInvariant(),
            Exp = expiresAt.ToUnixTimeSeconds(),
        };

        var payloadSegment = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        // The certificate is owned by the CA (GetTrustedCaCertificates does not transfer ownership),
        // so it is not disposed here; the RSA key handle obtained from it is.
        var root = GetRootCertificate(certificateAuthority);
        using var key = root.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The component CA root key is not available for signing.");
        var signature = key.SignData(
            Encoding.ASCII.GetBytes(payloadSegment), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return payloadSegment + "." + Base64Url.EncodeToString(signature);
    }

    /// <summary>Verifies the token's signature and decodes its payload; returns null when invalid.</summary>
    public static EnrollmentTokenContent? TryRead(IComponentCertificateAuthority certificateAuthority, string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return null;

        try
        {
            // Accept a signature from any currently-trusted CA root, so a token stays redeemable
            // across a CA rollover (the trust bundle may hold more than one generation).
            if (!VerifiesAgainstAnyRoot(certificateAuthority, Encoding.ASCII.GetBytes(parts[0]),
                    Base64Url.DecodeFromChars(parts[1])))
                return null;

            var payload = JsonSerializer.Deserialize<TokenPayload>(Base64Url.DecodeFromChars(parts[0]));
            // Jti length matches the persisted column; type must be a defined enum member; the bound
            // host FQDN must be present. (A token is CA-signed, so this only guards against a malformed
            // token we ourselves could have minted.)
            if (payload?.Jti is null || payload.Jti.Length is 0 or > 64
                || !Enum.IsDefined(payload.Type)
                || string.IsNullOrWhiteSpace(payload.Fqdn))
                return null;

            return new EnrollmentTokenContent(
                payload.Jti, payload.Type, payload.Fqdn, DateTimeOffset.FromUnixTimeSeconds(payload.Exp));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    private static bool VerifiesAgainstAnyRoot(
        IComponentCertificateAuthority certificateAuthority, byte[] payload, byte[] signature)
    {
        var any = false;
        foreach (var certificate in certificateAuthority.GetTrustedCaCertificates())
        {
            any = true;
            // The certificate is owned by the CA; only the RSA key handle is disposed here.
            using var key = certificate.GetRSAPublicKey();
            if (key is not null && key.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                return true;
        }

        if (!any)
            throw new InvalidOperationException(
                "No component CA root certificate is available to verify enrollment tokens.");
        return false;
    }

    private static X509Certificate2 GetRootCertificate(IComponentCertificateAuthority certificateAuthority)
    {
        foreach (var certificate in certificateAuthority.GetTrustedCaCertificates())
            return certificate;
        throw new InvalidOperationException(
            "No component CA root certificate is available to sign/verify enrollment tokens.");
    }

    private sealed class TokenPayload
    {
        [JsonPropertyName("jti")] public string? Jti { get; init; }
        [JsonPropertyName("type")] public ComponentType Type { get; init; }
        [JsonPropertyName("fqdn")] public string? Fqdn { get; init; }
        [JsonPropertyName("exp")] public long Exp { get; init; }
    }
}
