using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Eryph.Messages.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default <see cref="IEnrollmentTokenService"/>: a token is a payload (<c>jti</c>, bound component
/// type, expiry) signed by the component CA's root key and verified with the root certificate, so it
/// is self-validating — no token secret is stored. One-time use is enforced via the singleton
/// <see cref="IRedeemedTokenStore"/>; combined with a short lifetime this bounds replay.
/// </summary>
/// <remarks>
/// NOTE: the token is signed with the CA <i>root</i> key (the same key that signs the intermediates).
/// This is accepted because the signed payload is entirely server-generated here (Mint is not an
/// oracle for caller-supplied bytes), so it is not a cross-protocol signing oracle. A dedicated
/// enrollment-signing key would be cleaner and is a possible follow-up.
/// </remarks>
public sealed class EnrollmentTokenService(
    IComponentCertificateAuthority certificateAuthority,
    IRedeemedTokenStore redeemedTokens)
    : IEnrollmentTokenService
{
    public string Mint(ComponentType componentType, TimeSpan validFor)
    {
        var payload = new TokenPayload
        {
            Jti = Guid.NewGuid().ToString("N"),
            Type = componentType,
            Exp = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds(),
        };

        var payloadSegment = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        // The certificate is owned by the CA/store (GetTrustedCaCertificates does not transfer
        // ownership), so it is not disposed here; the RSA key handle we obtain from it is.
        var cert = GetRootCertificate();
        using var key = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The component CA root key is not available for signing.");
        var signature = key.SignData(
            Encoding.ASCII.GetBytes(payloadSegment), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return payloadSegment + "." + Base64Url.EncodeToString(signature);
    }

    public EnrollmentTokenValidationResult Redeem(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return EnrollmentTokenValidationResult.Invalid;

        var parts = token.Split('.');
        if (parts.Length != 2)
            return EnrollmentTokenValidationResult.Invalid;

        TokenPayload? payload;
        try
        {
            var cert = GetRootCertificate();
            using var key = cert.GetRSAPublicKey()
                ?? throw new InvalidOperationException("The component CA root certificate is not available.");
            var signatureValid = key.VerifyData(
                Encoding.ASCII.GetBytes(parts[0]),
                Base64Url.DecodeFromChars(parts[1]),
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            if (!signatureValid)
                return EnrollmentTokenValidationResult.Invalid;

            payload = JsonSerializer.Deserialize<TokenPayload>(Base64Url.DecodeFromChars(parts[0]));
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException or JsonException)
        {
            return EnrollmentTokenValidationResult.Invalid;
        }

        if (payload is null || payload.Jti is null)
            return EnrollmentTokenValidationResult.Invalid;

        var expiry = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (expiry <= DateTimeOffset.UtcNow)
            return EnrollmentTokenValidationResult.Invalid;

        // Claim the token id exactly once (the store is a singleton — see IRedeemedTokenStore).
        return redeemedTokens.TryRedeem(payload.Jti, expiry)
            ? EnrollmentTokenValidationResult.Valid(payload.Type)
            : EnrollmentTokenValidationResult.Invalid;
    }

    private X509Certificate2 GetRootCertificate()
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
        [JsonPropertyName("exp")] public long Exp { get; init; }
    }
}
