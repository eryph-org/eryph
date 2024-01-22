using System;
using System.Linq;
using Eryph.GenePool.Model;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneSetInfo(GeneSetIdentifier Id, string LocalPath, GenesetTagManifestData MetaData)
{
    public readonly GeneSetIdentifier Id = Id;
    public readonly string LocalPath = LocalPath;
    public readonly GenesetTagManifestData MetaData = MetaData;

}