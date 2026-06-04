using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Coerces an arbitrary entity id (client id, token id, jti) into a safe, collision-resistant file name. A
/// clean id is used verbatim; an id that has to be altered (invalid characters, empty/dot, or a Windows
/// reserved device name) gets a 64-bit hash suffix of the original id, so distinct ids are practically
/// certain to get distinct names and an export will not overwrite another entity's file. The seeded value
/// is still taken from the file contents, not the name. Both the export (write) and the change handler
/// (delete) call this same function, so they always agree on the name.
/// </summary>
internal static class IdentityConfigFileName
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    // The characters Windows forbids in a file name. Used regardless of host OS (rather than
    // Path.GetInvalidFileNameChars, which on Linux only reports '/' and NUL) so an export taken on the
    // standalone identity host (Linux) stays valid when restored on Windows (eryph-zero).
    private static readonly HashSet<char> InvalidFileNameChars =
        [.. Enumerable.Range(0, 32).Select(i => (char)i), '<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    public static string Coerce(string id)
    {
        id ??= "";
        var sanitized = new string(id.Select(c => InvalidFileNameChars.Contains(c) ? '_' : c).ToArray());

        // Empty, dot names and Windows reserved device names (e.g. CON, NUL, COM1 — even with an
        // extension) cannot be used as file names on Windows (eryph-zero). Prefix with '_' to make them
        // safe.
        if (string.IsNullOrEmpty(sanitized) || sanitized is "." or ".."
            || IsReservedDeviceName(sanitized))
            sanitized = "_" + sanitized;

        // Sanitizing is not injective (e.g. "a/b" and "a:b" both become "a_b"), which would let one
        // export overwrite another. Whenever the name had to change, append a hash of the original id to
        // keep names collision-resistant. Clean ids (the common case: GUID jtis, well-formed client ids)
        // keep their verbatim name.
        if (sanitized != id)
            sanitized = sanitized + "-" + Hash(id);

        return sanitized;
    }

    private static bool IsReservedDeviceName(string name)
    {
        var dot = name.IndexOf('.');
        var stem = dot >= 0 ? name[..dot] : name;
        return ReservedDeviceNames.Contains(stem);
    }

    // A stable (cross-process) 64-bit hash of the original id. SHA-256 rather than string.GetHashCode,
    // which is randomized per process and would not match on a later delete.
    private static string Hash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
