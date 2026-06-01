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
}
