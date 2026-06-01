using System.Threading;
using System.Threading.Tasks;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default enrollment policy: authorizes a request that presents a valid one-time enrollment token
/// (see <see cref="IEnrollmentTokenRedeemer"/>) whose bound component type matches the request.
/// Redeeming the token marks it used, so it cannot enroll a second component. If no/invalid token is
/// presented the request is denied — enrollment must be deliberately authorized by an operator-minted
/// token.
/// </summary>
/// <remarks>
/// The token is bound to a single component type AND host FQDN, so it can enroll only the one host the
/// operator named. It does NOT <i>authenticate</i> that the caller is that host — the FQDN is
/// self-reported by the component and the server-derived component id reflects that claimed type/FQDN.
/// Enterprise deployments can replace this with an <see cref="IComponentEnrollmentPolicy"/> that
/// authenticates the host (attestation, cloud instance identity, …).
/// </remarks>
public sealed class TokenEnrollmentPolicy(
    IEnrollmentTokenRedeemer tokenRedeemer,
    ILogger<TokenEnrollmentPolicy> logger)
    : IComponentEnrollmentPolicy
{
    public async Task<bool> IsAuthorizedAsync(
        ComponentEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Token))
        {
            logger.LogWarning("Rejecting component enrollment: no enrollment token was presented.");
            return false;
        }

        // The redeemer consumes the token only if it is valid AND bound to the requested type and host
        // FQDN, so a wrong-type/wrong-host request cannot burn a still-valid token. The component
        // asserts its own FQDN (ComponentEnrollmentClient sends identity.MachineName); the operator
        // minted the token for exactly that host. The reason is not surfaced to the caller.
        var result = await tokenRedeemer.RedeemAsync(
            request.Token, request.ComponentType, request.Fqdn ?? "", cancellationToken);
        if (!result.IsValid)
        {
            logger.LogWarning(
                "Rejecting component enrollment: the enrollment token is invalid, expired, already used, or not bound to the requested type and host.");
            return false;
        }

        return true;
    }
}
