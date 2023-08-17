using System;
using System.Linq;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GeneSetInfo(GeneSetIdentifier Id, string LocalPath, GeneSetManifestData MetaData)
{
    public readonly GeneSetIdentifier Id = Id;
    public readonly string LocalPath = LocalPath;
    public readonly GeneSetManifestData MetaData = MetaData;

}