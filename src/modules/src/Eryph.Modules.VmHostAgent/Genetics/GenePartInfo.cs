using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using JetBrains.Annotations;

namespace Eryph.Modules.VmHostAgent.Genetics;

public record GenePartInfo(
    GeneType GeneType,
    GeneIdentifier GeneId,
    GeneArchitecture Architecture,
    string PartHash,
    string? Path,
    long? Size)
{
    public override string ToString() => $"{GeneType} {GeneId} ({Architecture})/{PartHash}";
}
