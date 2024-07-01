using Eryph.ConfigModel;
using Eryph.GenePool.Model;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface ILocalGenepoolReader
{
    public Either<Error, Option<GeneSetIdentifier>> GetGenesetReference(GeneSetIdentifier geneset);

    public Either<Error, string> ReadGeneContent(GeneType geneType, GeneIdentifier gene);
}