using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Coerces an arbitrary entity id (client id, token id, jti) into a safe file name. The seeded value is
/// always taken from the file contents, not the name, so a (rare) coercion collision only affects
/// delete-by-name, never the rebuilt data. Both the export (write) and the change handler (delete) use
/// this same function, so they always agree on the name.
/// </summary>
internal static class IdentityConfigFileName
{
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public static string Coerce(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string((id ?? "").Select(c => invalid.Contains(c) ? '_' : c).ToArray());

        // Empty, dot names and Windows reserved device names (e.g. CON, NUL, COM1 — even with an
        // extension) cannot be used as file names on Windows (eryph-zero). Prefix with '_' to make them
        // safe and still distinct.
        if (string.IsNullOrEmpty(sanitized) || sanitized is "." or ".."
            || IsReservedDeviceName(sanitized))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    private static bool IsReservedDeviceName(string name)
    {
        var dot = name.IndexOf('.');
        var stem = dot >= 0 ? name[..dot] : name;
        return ReservedDeviceNames.Contains(stem);
    }
}
