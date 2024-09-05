using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class GenePoolPaths
{
    public static string GetGenePoolPath(
        VmHostAgentConfiguration vmHostAgentConfig) =>
        Path.Combine(vmHostAgentConfig.Defaults.Volumes, "genepool");

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
        Path.Combine(GetGeneSetPath(genePoolPath, geneSetId), "geneset-tag.json");

    public static string GetGenePath(
        string genePoolPath,
        GeneType geneType,
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

        return Path.Combine(GetGeneSetPath(genePoolPath, geneId.GeneSet),
            geneFolder,
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
                string.Equals(Path.GetFileName(geneSetManifestPath), "geneset-tag.json",
                    StringComparison.OrdinalIgnoreCase),
                Error.New("The gene set manifest path does not point to a gene set manifest."))
            .ToEither()
        from geneSetId in GetGeneSetIdFromPath(genePoolPath, Path.GetDirectoryName(geneSetManifestPath))
        select geneSetId;
}
