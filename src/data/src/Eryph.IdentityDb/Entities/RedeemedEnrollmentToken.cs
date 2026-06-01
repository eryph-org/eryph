using System;

namespace Eryph.IdentityDb.Entities;

/// <summary>
/// Records a component enrollment token that has been redeemed. The primary key on
/// <see cref="Jti"/> is what makes a token single use: a second redemption of the same token id
/// cannot insert the row again.
/// </summary>
public class RedeemedEnrollmentToken
{
    /// <summary>The token's unique id (its <c>jti</c>); primary key.</summary>
    public string Jti { get; set; } = "";

    /// <summary>When the token expires; redeemed rows past this point are pruned on the next redeem.</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
