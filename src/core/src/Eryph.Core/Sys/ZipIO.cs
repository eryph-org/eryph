using LanguageExt;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt.Common;
using static LanguageExt.Prelude;

namespace Eryph.Core.Sys;

public interface ZipIO
{
    Seq<ZipArchiveEntryMetadata> GetEntries(string archivePath);

    ValueTask<byte[]> GetEntryContent(string archivePath, string entryName);

    ValueTask<Unit> ExtractToDirectory(string archivePath, string destinationPath);
}

public readonly struct LiveZipIO : ZipIO
{
    public static readonly ZipIO Default = new LiveZipIO();

    public Seq<ZipArchiveEntryMetadata> GetEntries(string archivePath)
    {
        using var zipArchive = ZipFile.OpenRead(archivePath);
        return zipArchive.Entries.ToSeq()
            .Map(e => new ZipArchiveEntryMetadata(
                e.FullName,
                e.Length,
                e.Crc32))
            .Strict();
    }

    public async ValueTask<Unit> ExtractToDirectory(string archivePath, string destinationPath)
    {
        await Task.Run(() => ZipFile.ExtractToDirectory(archivePath, destinationPath)).ConfigureAwait(false);
        return unit;
    }

    public async ValueTask<byte[]> GetEntryContent(string archivePath, string entryName)
    {
        using var zipArchive = ZipFile.OpenRead(archivePath);
        var entry = zipArchive.GetEntry(entryName);
        if (entry is null)
            throw Error.New($"The ZIP archive does not contain the entry '{entryName}'.");
        
        using var memoryStream = new MemoryStream();
        await using var entryStream = entry.Open();
        await entryStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}

