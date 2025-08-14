using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using LanguageExt;

namespace Eryph.Modules.GenePool.Genetics;

public record GenePartsInfo(
    UniqueGeneIdentifier Id,
    GeneHash Hash,
    Seq<GenePartHash> Parts,
    HashMap<GenePartHash, long> ExistingParts);
