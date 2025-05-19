using System.Security.AccessControl;
using LanguageExt;

namespace Eryph.Core.Sys;

public static class FileSystem<RT>
    where RT : struct, HasFileSystem<RT>
{
    public static Aff<RT, uint> getCrc32(string path) =>
        default(RT).FileSystemEff.MapAsync(e => e.GetCrc32(path));

    public static Aff<RT, bool> isInUse(string path) =>
        default(RT).FileSystemEff.MapAsync(e => e.IsInUse(path));

    public static Aff<RT, Unit> extractToDirectory(
        string archivePath,
        string destinationPath) =>
        default(RT).FileSystemEff.MapAsync(e => e.ExtractToDirectory(archivePath, destinationPath));

    public static Eff<RT, Unit> setAccessControl(
        string path,
        DirectorySecurity directorySecurity) =>
        default(RT).FileSystemEff.Map(e => e.SetAccessControl(path, directorySecurity));
}
