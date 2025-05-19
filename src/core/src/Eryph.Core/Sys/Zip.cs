using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.Core.Sys;

public static class Zip<RT>
    where RT : struct, HasZip<RT>
{
    public static Aff<RT, Unit> extractToDirectory(string archivePath, string destinationPath) =>
        default(RT).ZipEff.MapAsync(e => e.ExtractToDirectory(archivePath, destinationPath));

    public static Eff<RT, Seq<ZipArchiveEntryMetadata>> getEntries(string archivePath) =>
        default(RT).ZipEff.Map(e => e.GetEntries(archivePath));

    public static Aff<RT, byte[]> getEntryContent(string archivePath, string entryName) =>
        default(RT).ZipEff.MapAsync(e => e.GetEntryContent(archivePath, entryName));
}
