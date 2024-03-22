using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneSetInfo(GeneSetIdentifier Id, string LocalPath, GenesetTagManifestData MetaData,
    GetGeneDownloadResponse[] GeneDownloadInfo)
{
    public readonly GeneSetIdentifier Id = Id;
    public readonly string LocalPath = LocalPath;
    public readonly GenesetTagManifestData MetaData = MetaData;
    public readonly GetGeneDownloadResponse[] GeneDownloadInfo = GeneDownloadInfo;
}