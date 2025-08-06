using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using Eryph.VmManagement;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;

namespace Eryph.Modules.GenePool.Genetics;

internal class GenePoolReaderWithCache(
    IGenePoolPathProvider genePoolPathProvider,
    IGenePoolFactory genePoolFactory,
    ILogger logger)
    : IGenePoolReader
{
    public EitherAsync<Error, string> GetGeneContent(
        UniqueGeneIdentifier uniqueGeneId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public EitherAsync<Error, GenesetTagManifestData> GetGeneSetTagManifest(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(
        GeneSetIdentifier geneSetId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
