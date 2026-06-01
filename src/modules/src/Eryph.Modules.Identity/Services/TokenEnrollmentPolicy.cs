using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default enrollment policy: authorizes a request that presents a valid one-time enrollment token
/// (see <see cref="IEnrollmentTokenService"/>) whose bound component type matches the request.
/// Redeeming the token marks it used, so it cannot enroll a second component. If no/invalid token is
/// presented the request is denied — enrollment must be deliberately authorized by an operator-minted
/// token.
/// </summary>
/// <remarks>
/// The token proves the operator authorized this enrollment; it does NOT prove the caller's host
/// identity (the server-derived component id reflects the claimed type/FQDN). Enterprise deployments
/// can replace this with an <see cref="IComponentEnrollmentPolicy"/> that authenticates the host
/// (attestation, cloud instance identity, …).
/// </remarks>
public sealed class TokenEnrollmentPolicy(
    IEnrollmentTokenService tokenService,
    ILogger<TokenEnrollmentPolicy> logger)
    : IComponentEnrollmentPolicy
{
    public bool IsAuthorized(ComponentEnrollmentRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            logger.LogWarning("Rejecting component enrollment: no enrollment token was presented.");
            return false;
        }

        var result = tokenService.Redeem(request.Token);
        if (!result.IsValid)
        {
            logger.LogWarning("Rejecting component enrollment: the enrollment token is invalid, expired or already used.");
            return false;
        }

        if (result.ComponentType != request.ComponentType)
        {
            logger.LogWarning(
                "Rejecting component enrollment: token is bound to {TokenType} but the request is for {RequestType}.",
                result.ComponentType, request.ComponentType);
            return false;
        }

        return true;
    }
}
