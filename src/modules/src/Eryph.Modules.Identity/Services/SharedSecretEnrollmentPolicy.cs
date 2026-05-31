using System.Security.Cryptography;
using System.Text;
using Eryph.ModuleCore.Components;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Default enrollment policy: authorizes a request when its credential matches the
/// operator-provisioned enrollment secret (compared in constant time). If no secret is configured
/// the policy denies every request — a secure default, since enrollment must be deliberately
/// enabled by provisioning the secret. Enterprise deployments replace this with their own
/// <see cref="IComponentEnrollmentPolicy"/> (the secret itself is provisioned by the out-of-repo tooling).
/// </summary>
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
