using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Eryph.VmManagement;

public class GenePoolReader : IGenePoolReader
{
    public EitherAsync<Error, string> GetGeneContent(UniqueGeneIdentifier uniqueGeneId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public EitherAsync<Error, GenesetTagManifestData> GetGeneSetTagManifest(GeneSetIdentifier geneSetId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public EitherAsync<Error, GeneSetIdentifier> ResolveGeneSet(GeneSetIdentifier geneSetId, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
