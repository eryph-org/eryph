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
        string token, ComponentType expectedComponentType, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Drop redeemed rows whose tokens have expired; they can never be redeemed again anyway.
        var expired = await redeemedTokens.ListAsync(new EnrollmentTokenSpecs.Expired(now), cancellationToken);
        if (expired.Count > 0)
            await redeemedTokens.DeleteRangeAsync(expired, cancellationToken);

        // Reject (without consuming the token) before claiming, so a wrong-type or otherwise-invalid
        // request cannot burn a one-time token that is still valid for its intended component.
        var content = EnrollmentTokenCodec.TryRead(certificateAuthority, token);
        if (content is null || content.ExpiresAt <= now || content.ComponentType != expectedComponentType
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
        catch (DbUpdateException)
        {
            // A concurrent redemption claimed the same jti first.
            return EnrollmentTokenValidationResult.Invalid;
        }

        return EnrollmentTokenValidationResult.Valid(content.ComponentType);
    }
}
