using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.StateDb.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.Controller.Compute;

internal static class StateStoreGeneExtensions
{
    public static Either<Error, GeneIdentifierWithType> ToGeneIdWithType(this Gene dbGene) =>
        from geneSetId in GeneSetIdentifier.NewEither(dbGene.GeneSet)
        from geneName in GeneName.NewEither(dbGene.Name)
        let geneId = new GeneIdentifier(geneSetId, geneName)
        select new GeneIdentifierWithType(dbGene.GeneType, geneId);
}
