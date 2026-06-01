using System;
using Ardalis.Specification;
using Eryph.IdentityDb.Entities;

namespace Eryph.IdentityDb.Specifications;

public static class EnrollmentTokenSpecs
{
    /// <summary>Redeemed tokens that have expired and can be removed.</summary>
    public sealed class Expired : Specification<RedeemedEnrollmentToken>
    {
        public Expired(DateTimeOffset now)
        {
            Query.Where(t => t.ExpiresAt <= now);
        }
    }

    /// <summary>
    /// A redeemed token by its <c>jti</c>, queried without change tracking so it reflects the database
    /// (not entities pending in the tracker) — used to tell a concurrent redemption apart from an
    /// unrelated save failure.
    /// </summary>
    public sealed class ByJti : Specification<RedeemedEnrollmentToken>
    {
        public ByJti(string jti)
        {
            Query.Where(t => t.Jti == jti).AsNoTracking();
        }
    }
}
