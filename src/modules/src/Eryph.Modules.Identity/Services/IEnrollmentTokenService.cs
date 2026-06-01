using Eryph.Messages.Components;

namespace Eryph.Modules.Identity.Services;

/// <summary>
/// Mints and validates one-time component enrollment tokens. A token is signed by the component CA
/// (so it is self-validating — there is no plaintext token store) and bound to a single
/// <see cref="ComponentType"/> with an expiry; it may be redeemed at most once.
/// </summary>
public interface IEnrollmentTokenService
{
    /// <summary>Mints a signed, single-use token authorizing enrollment of the given component type.</summary>
    string Mint(ComponentType componentType, System.TimeSpan validFor);

    /// <summary>
    /// Validates a presented token (signature, expiry, not-yet-redeemed) and, on success, marks it
    /// redeemed so it cannot be reused. Returns the bound component type when valid.
    /// </summary>
    EnrollmentTokenValidationResult Redeem(string token);
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
