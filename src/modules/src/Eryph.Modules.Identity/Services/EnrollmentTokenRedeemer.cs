using System;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.IdentityDb.Specifications;
using Eryph.Messages.Components;
using Microsoft.EntityFrameworkCore;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default <see cref="IEnrollmentTokenRedeemer"/>. The token is verified against the component CA
/// (<see cref="EnrollmentTokenCodec"/>); single use is enforced by recording the token's <c>jti</c>
/// in <see cref="RedeemedEnrollmentToken"/>, whose primary key rejects a second redemption.
/// </summary>
public sealed class EnrollmentTokenRedeemer(
    IComponentCertificateAuthority certificateAuthority,
    IIdentityDbRepository<RedeemedEnrollmentToken> redeemedTokens)
    : IEnrollmentTokenRedeemer
{
    public async Task<EnrollmentTokenValidationResult> RedeemAsync(
        string token,
        ComponentType expectedComponentType,
        string expectedFqdn,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Drop redeemed rows whose tokens have expired; they can never be redeemed again anyway.
        var expired = await redeemedTokens.ListAsync(new EnrollmentTokenSpecs.Expired(now), cancellationToken);
        if (expired.Count > 0)
            await redeemedTokens.DeleteRangeAsync(expired, cancellationToken);

        // Reject (without consuming the token) before claiming, so a wrong-type, wrong-host or
        // otherwise-invalid request cannot burn a one-time token that is still valid for its intended
        // component. The token is bound to a single component type AND host FQDN (DNS-insensitive).
        var content = EnrollmentTokenCodec.TryRead(certificateAuthority, token);
        if (content is null || content.ExpiresAt <= now
                            || content.ComponentType != expectedComponentType
                            || !string.Equals(content.Fqdn, expectedFqdn, StringComparison.OrdinalIgnoreCase)
                            || await redeemedTokens.GetByIdAsync(content.Jti, cancellationToken) is not null)
        {
            // Commit the prune (only when there was something to prune) even though the token is rejected.
            if (expired.Count > 0)
                await redeemedTokens.SaveChangesAsync(cancellationToken);
            return EnrollmentTokenValidationResult.Invalid;
        }

        await redeemedTokens.AddAsync(
            new RedeemedEnrollmentToken { Jti = content.Jti, ExpiresAt = content.ExpiresAt },
            cancellationToken);
        try
        {
            await redeemedTokens.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException updateEx)
        {
            // The insert may have failed because a concurrent redemption claimed the same jti first, or
            // for an unrelated reason (connectivity, schema). Distinguish them by reading the database
            // (AnyAsync issues an EXISTS query, ignoring our failed Added entity in the change tracker):
            // if the row is now present it was a concurrent redemption and the token is spent; otherwise
            // rethrow so a real database failure surfaces as an operational error, not a false "invalid".
            bool alreadyRedeemed;
            try
            {
                alreadyRedeemed = await redeemedTokens.AnyAsync(
                    new EnrollmentTokenSpecs.ByJti(content.Jti), cancellationToken);
            }
            catch
            {
                // The re-check itself failed (e.g. the database is unreachable); surface the original
                // update failure rather than masking it with this secondary error.
                throw updateEx;
            }

            if (alreadyRedeemed)
                return EnrollmentTokenValidationResult.Invalid;
            throw;
        }

        return EnrollmentTokenValidationResult.Valid(content.ComponentType);
    }
}
