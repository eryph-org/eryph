using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

public static class GeneManifestUtils
{
    public static Either<Error, Seq<GenePartHash>> GetParts(
        GeneManifestData manifest) =>
        from parts in manifest.Parts.ToSeq()
            .Map(GenePartHash.NewEither)
            .Sequence()
        select parts;
}
