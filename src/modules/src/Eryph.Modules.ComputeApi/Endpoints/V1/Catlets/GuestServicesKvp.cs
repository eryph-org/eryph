using System;
using System.Globalization;
using Eryph.GuestServices.Core;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

// Helpers for building guest-services External-pool writes: the per-subject
// access-key slot name (from the guest-services contract) and the
// authorized_keys line format, so every write endpoint produces the same wire
// format. Shell keys are taken directly from <see cref="Constants"/>.
internal static class GuestServicesKvp
{
    // Per-subject authorized-key slot the guest service reads.
    public static string AccessKeySlot(string subjectId) => Constants.ClientAuthKeyPrefix + subjectId;

    // OpenSSH authorized_keys line with an optional leading expiry-time option.
    // The timestamp must be the OpenSSH compact UTC form yyyyMMddHHmmssZ — the
    // guest's key provider rejects other forms and treats them as expired.
    public static string BuildAuthorizedKeyLine(string publicKey, DateTimeOffset? keyExpiry)
    {
        var key = publicKey.Trim();
        if (keyExpiry is not { } expiry)
            return key;

        var expiryText = expiry.ToUniversalTime().ToString("yyyyMMddHHmmss'Z'", CultureInfo.InvariantCulture);
        return $"expiry-time=\"{expiryText}\" {key}";
    }
}
