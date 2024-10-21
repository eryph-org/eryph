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
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;

namespace Eryph.Modules.Controller.Compute;

internal static class StateStoreGeneExtensions
{
    public static Either<Error, UniqueGeneIdentifier> ToUniqueGeneId(this Gene dbGene) =>
        from geneId in GeneIdentifier.NewEither(dbGene.GeneId)
        from architecture in Architecture.NewEither(dbGene.Architecture)
        select new UniqueGeneIdentifier(dbGene.GeneType, geneId, architecture);
}
