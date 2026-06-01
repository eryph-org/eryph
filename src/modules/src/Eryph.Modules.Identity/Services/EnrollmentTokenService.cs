using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Linq;
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
/// is self-validating — no token secret is stored. One-time use is enforced by recording redeemed
/// <c>jti</c>s; combined with a short lifetime this bounds replay.
/// </summary>
/// <remarks>
/// The redeemed-id set is in-memory: an identity restart within a token's (short) lifetime would
/// allow that token to be redeemed again. Persisting redeemed ids across restarts is a follow-up;
/// the short token lifetime is the interim mitigation.
/// </remarks>
public sealed class EnrollmentTokenService(IComponentCertificateAuthority certificateAuthority)
    : IEnrollmentTokenService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _redeemed = new();

    public string Mint(ComponentType componentType, TimeSpan validFor)
    {
        var payload = new TokenPayload
        {
            Jti = Guid.NewGuid().ToString("N"),
            Type = componentType,
            Exp = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds(),
        };

        var payloadSegment = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        using var key = GetRootCertificate().GetRSAPrivateKey()
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
            using var key = GetRootCertificate().GetRSAPublicKey()
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

        // Atomically claim the token id; a second redemption fails.
        if (!_redeemed.TryAdd(payload.Jti, expiry))
            return EnrollmentTokenValidationResult.Invalid;

        PruneExpired();
        return EnrollmentTokenValidationResult.Valid(payload.Type);
    }

    private System.Security.Cryptography.X509Certificates.X509Certificate2 GetRootCertificate() =>
        certificateAuthority.GetTrustedCaCertificates().FirstOrDefault()
        ?? throw new InvalidOperationException(
            "No component CA root certificate is available to sign/verify enrollment tokens.");

    private void PruneExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in _redeemed.Where(e => e.Value <= now).ToList())
            _redeemed.TryRemove(entry.Key, out _);
    }

    private sealed class TokenPayload
    {
        [JsonPropertyName("jti")] public string? Jti { get; init; }
        [JsonPropertyName("type")] public ComponentType Type { get; init; }
        [JsonPropertyName("exp")] public long Exp { get; init; }
    }
}
