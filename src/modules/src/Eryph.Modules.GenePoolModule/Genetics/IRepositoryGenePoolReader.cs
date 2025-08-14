using Eryph.ConfigModel;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

public interface IRepositoryGenePoolReader
{
    EitherAsync<Error, Option<GeneSetInfo>> ProvideGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken);

    EitherAsync<Error, Option<GeneContentInfo>> ProvideGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        GeneHash geneHash,
        CancellationToken cancellationToken);
}
