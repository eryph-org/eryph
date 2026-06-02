using System.IO;
using System.Linq;

namespace Eryph.Modules.Identity.ChangeTracking;

/// <summary>
/// Coerces an arbitrary entity id (client id, token id, jti) into a safe file name. The seeded value is
/// always taken from the file contents, not the name, so a (rare) coercion collision only affects
/// delete-by-name, never the rebuilt data.
/// </summary>
internal static class IdentityConfigFileName
{
    public static string Coerce(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
