using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Modules.GenePool.Genetics;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class GenePoolPaths
{
    private const string GeneSetManifestFileName = "geneset-tag.json";
    private const string InvalidGenePathError = "The gene path is invalid.";

    public static bool IsPathInGenePool(string genePoolPath, string path) =>
        PathUtils.GetContainedPath(genePoolPath, path).IsSome;

    public static string GetGeneSetPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId) =>
        Path.Combine(genePoolPath,
            geneSetId.Organization.Value,
            geneSetId.GeneSet.Value,
            geneSetId.Tag.Value);

    public static string GetGeneSetManifestPath(
        string genePoolPath,
        GeneSetIdentifier geneSetId) =>
        Path.Combine(GetGeneSetPath(genePoolPath, geneSetId), GeneSetManifestFileName);

    public static string GetGenePath(
        string genePoolPath,
        UniqueGeneIdentifier uniqueGeneId) =>
        GetGenePath(genePoolPath, uniqueGeneId.GeneType, uniqueGeneId.Architecture, uniqueGeneId.Id);

    public static string GetTempGeneManifestPath(
        string geneDownloadPath,
        GeneHash geneHash) =>
        Path.Combine(geneDownloadPath, geneHash.Value, "gene.json");

    public static string GetTempGenePartPath(
        string geneDownloadPath,
        GeneHash geneHash,
        GenePartHash genePartHash) =>
        Path.Combine(geneDownloadPath, geneHash.Value, $"{genePartHash}.part");

    public static string GetGenePath(
        string genePoolPath,
        GeneType geneType,
        Architecture architecture,
        GeneIdentifier geneId)
    {
        var geneFolder = geneType switch
        {
            GeneType.Catlet => "",
            GeneType.Volume => "volumes",
            GeneType.Fodder => "fodder",
            _ => throw new ArgumentException($"The gene type '{geneType}' is not supported",
                nameof(geneType)),
        };

        var extension = geneType switch
        {
            GeneType.Catlet => "json",
            GeneType.Volume => "vhdx",
            GeneType.Fodder => "json",
            _ => throw new ArgumentException($"The gene type '{geneType}' is not supported",
                nameof(geneType)),
        };

        return Path.Combine(
            GetGeneSetPath(genePoolPath, geneId.GeneSet),
            geneFolder,
            architecture.Hypervisor.IsAny ? "" : architecture.Hypervisor.Value,
            architecture.ProcessorArchitecture.IsAny ? "" : architecture.ProcessorArchitecture.Value,
            $"{geneId.GeneName}.{extension}");
    }

    public static Either<Error, GeneSetIdentifier> GetGeneSetIdFromPath(
        string genePoolPath,
        string geneSetPath) =>
        from _1 in guard(Path.IsPathFullyQualified(genePoolPath),
                Error.New("The gene pool path is not fully qualified."))
            .ToEither()
        from _2 in guard(Path.IsPathFullyQualified(geneSetPath),
            Error.New("The gene set path is not fully qualified."))
        from containedPath in PathUtils.GetContainedPath(genePoolPath, geneSetPath)
            .ToEither(Error.New("The gene set path is not located in the gene pool."))
        let parts = containedPath.Split(Path.DirectorySeparatorChar)
        from _3 in guard(parts.Length == 3,
            Error.New("The gene set path is invalid."))
        from organizationName in OrganizationName.NewEither(parts[0])
        from geneSetName in GeneSetName.NewEither(parts[1])
        from tagName in TagName.NewEither(parts[2])
        select new GeneSetIdentifier(organizationName, geneSetName, tagName);

    public static Either<Error, GeneSetIdentifier> GetGeneSetIdFromManifestPath(
        string genePoolPath,
        string geneSetManifestPath) =>
        from _1 in guard(
                string.Equals(Path.GetFileName(geneSetManifestPath), GeneSetManifestFileName,
                    StringComparison.OrdinalIgnoreCase),
                Error.New("The gene set manifest path does not point to a gene set manifest."))
            .ToEither()
        from geneSetId in GetGeneSetIdFromPath(genePoolPath, Path.GetDirectoryName(geneSetManifestPath))
        select geneSetId;

    public static Either<Error, UniqueGeneIdentifier> GetUniqueGeneIdFromPath(
        string genePoolPath,
        string genePath) =>
        from _1 in guard(Path.IsPathFullyQualified(genePoolPath),
                Error.New("The gene pool path is not fully qualified."))
            .ToEither()
        from _2 in guard(Path.IsPathFullyQualified(genePath),
            Error.New("The gene path is not fully qualified."))
        from containedPath in PathUtils.GetContainedPath(genePoolPath, genePath)
            .ToEither(Error.New("The gene path is not located in the gene pool."))
        let parts = containedPath.Split(Path.DirectorySeparatorChar)
        from _3 in guard(parts.Length is >= 4 and <= 7, Error.New(InvalidGenePathError))
        from organizationName in OrganizationName.NewEither(parts[0])
        from geneSetName in GeneSetName.NewEither(parts[1])
        from tagName in TagName.NewEither(parts[2])
        let geneSetId = new GeneSetIdentifier(organizationName, geneSetName, tagName)
        from geneType in parts[3].ToLowerInvariant() switch
        {
            "catlet.json" => Right<Error, GeneType>(GeneType.Catlet),
            "volumes" => Right(GeneType.Volume),
            "fodder" => Right(GeneType.Fodder),
            _ => Error.New(InvalidGenePathError)
        }
        from uniqueGeneId in geneType switch
        {
            GeneType.Catlet => new UniqueGeneIdentifier(
                GeneType.Catlet,
                new GeneIdentifier(geneSetId, GeneName.New("catlet")),
                Architecture.New("any")),
            GeneType.Volume or GeneType.Fodder => GetUniqueGeneIdFromSegments(geneType, geneSetId, parts[4..]),
            _ => Error.New(InvalidGenePathError)
        }
        select uniqueGeneId;

    private static Either<Error, UniqueGeneIdentifier> GetUniqueGeneIdFromSegments(
        GeneType geneType,
        GeneSetIdentifier geneSetId,
        string[] segments) =>
        from _ in guard(segments.Length is >= 1 and <= 3, Error.New(InvalidGenePathError))
            .ToEither()
        from architecture in GetArchitectureFromPathSegments(segments[..^1])
            .MapLeft(e => Error.New(InvalidGenePathError, e))
        let fileName = segments[^1]
        from __ in guard(Path.HasExtension(fileName), Error.New(InvalidGenePathError))
        let extension = Path.GetExtension(fileName)?.ToLowerInvariant()
        from _3 in guard(geneType is GeneType.Fodder && extension == ".json"
                         || geneType is GeneType.Volume && extension == ".vhdx",
            Error.New("The gene path is invalid"))
        from geneName in GeneName.NewEither(Path.GetFileNameWithoutExtension(fileName))
        select new UniqueGeneIdentifier(
            geneType,
            new GeneIdentifier(geneSetId, geneName),
            architecture);

    private static Either<Error, Architecture> GetArchitectureFromPathSegments(
        string[] segments) =>
        from result in segments.Length switch
        {
            0 => Architecture.New("any"),
            1 => from hypervisor in Hypervisor.NewEither(segments[0])
                let processorArchitecture = ProcessorArchitecture.New("any")
                select new Architecture(hypervisor, processorArchitecture),
            2 => from hypervisor in Hypervisor.NewEither(segments[0])
                from processorArchitecture in ProcessorArchitecture.NewEither(segments[1])
                select new Architecture(hypervisor, processorArchitecture),
            _ => Error.New("The gene architecture is invalid.")
        }
        select result;
}
