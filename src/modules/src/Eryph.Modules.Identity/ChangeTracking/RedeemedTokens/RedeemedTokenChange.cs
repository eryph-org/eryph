using System;

namespace Eryph.Modules.Identity.ChangeTracking.RedeemedTokens;

/// <summary>A change to a redeemed enrollment-token record, identified by its <c>jti</c>.</summary>
internal record RedeemedTokenChange(string Jti);

/// <summary>The on-disk form of a redeemed enrollment-token record.</summary>
internal record RedeemedTokenConfigModel
{
    public string Jti { get; init; } = "";

    public DateTimeOffset ExpiresAt { get; init; }
}
