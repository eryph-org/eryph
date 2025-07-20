using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneSetInfo(
    GeneSetIdentifier Id,
    GenesetTagManifestData MetaData,
    GetGeneDownloadResponse[] GeneDownloadInfo);
