using System;
using System.Linq;
using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using Eryph.GenePool.Model.Responses;

namespace Eryph.Modules.GenePool.Genetics;

public record GeneSetInfo(
    GeneSetIdentifier Id,
    GenesetTagManifestData Manifest,
    GetGeneDownloadResponse[] GeneDownloadInfo);
