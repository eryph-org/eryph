using System.Threading;
using System.Threading.Tasks;
using Eryph.Messages.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Redeems one-time component enrollment tokens. A token is signed by the component CA (so it is
/// self-validating — see <see cref="EnrollmentTokenCodec"/>) and bound to a single
/// <see cref="ComponentType"/> with an expiry; it may be redeemed at most once.
/// </summary>
public interface IEnrollmentTokenRedeemer
{
    /// <summary>
    /// Validates a presented token (signature, expiry, bound to <paramref name="expectedComponentType"/>,
    /// not-yet-redeemed) and, on success, records it redeemed so it cannot be reused. The token is
    /// consumed only when it is actually valid for the request, so a wrong-type or otherwise-rejected
    /// request does not burn the one-time token. Returns the bound component type when valid.
    /// </summary>
    Task<EnrollmentTokenValidationResult> RedeemAsync(
        string token, ComponentType expectedComponentType, CancellationToken cancellationToken = default);
}

/// <summary>The outcome of redeeming an enrollment token.</summary>
public sealed class EnrollmentTokenValidationResult
{
    public bool IsValid { get; init; }

    public ComponentType ComponentType { get; init; }

    public static EnrollmentTokenValidationResult Invalid { get; } = new() { IsValid = false };

    public static EnrollmentTokenValidationResult Valid(ComponentType componentType) =>
        new() { IsValid = true, ComponentType = componentType };
}
