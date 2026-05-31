using System.Security.Cryptography;
using System.Text;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default enrollment policy: authorizes a request when its credential matches the
/// operator-provisioned enrollment secret (compared in constant time). If no secret is configured
/// the policy denies every request — a secure default, since enrollment must be deliberately
/// enabled by provisioning the secret.
/// </summary>
/// <remarks>
/// LIMITATION: this policy authorizes only on knowledge of the secret; it does NOT bind the request
/// to the caller's real host identity. Any holder of the secret can therefore request a certificate
/// for any <c>ComponentType</c>/FQDN it chooses (i.e. impersonate another component) — the
/// server-derived component id reflects the *claimed* identity, not an authenticated one. This is
/// acceptable only when the secret is tightly scoped/short-lived. Enterprise deployments replace
/// this with an <see cref="IComponentEnrollmentPolicy"/> that authenticates the host (attestation,
/// per-host secret, cloud instance identity, …). The secret is provisioned by the out-of-repo tooling.
/// </remarks>
public sealed class SharedSecretEnrollmentPolicy(
    string? configuredSecret,
    ILogger<SharedSecretEnrollmentPolicy> logger)
    : IComponentEnrollmentPolicy
{
    public bool IsAuthorized(ComponentEnrollmentRequest request)
    {
        if (string.IsNullOrEmpty(configuredSecret))
        {
            logger.LogWarning(
                "Rejecting component enrollment: no enrollment secret is configured. Provision the "
                + "enrollment secret to enable component enrollment.");
            return false;
        }

        var provided = Encoding.UTF8.GetBytes(request?.Credential ?? "");
        var expected = Encoding.UTF8.GetBytes(configuredSecret);
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }
}
