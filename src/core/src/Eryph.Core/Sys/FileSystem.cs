using LanguageExt;

namespace Eryph.Core.Sys;

public static class FileSystem<RT>
    where RT : struct, HasFileSystem<RT>
{
    public static Aff<RT, bool> isInUse(string path) =>
        default(RT).FileSystemEff.MapAsync(e => e.IsInUse(path));
}
