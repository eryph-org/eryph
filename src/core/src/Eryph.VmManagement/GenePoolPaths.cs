using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Core.VmAgent;

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
        Path.Combine(GetGeneSetPath(genePoolPath, geneSetId), "manifest-tag.json");

    public static string GetGenePath(
        string genePoolPath,
        GeneType geneType,
        GeneIdentifier geneId)
    {
        var geneFolder = geneType switch
        {
            GeneType.Catlet => ".",
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
}
