using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core.Genetics;

public static class CatletPedigree
{
    public static Either<Error, (CatletConfig Config, Option<CatletConfig> ParentConfig)> Breed(
        CatletConfig config,
        HashMap<GeneSetIdentifier, GeneSetIdentifier> geneSetMap,
        HashMap<GeneSetIdentifier, CatletConfig> ancestors) =>
        from resolvedConfig in CatletGeneResolving.ResolveGenesetIdentifiers(config, geneSetMap)
        select (resolvedConfig, Option<CatletConfig>.None);
}
