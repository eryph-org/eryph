using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface ILocalGenepoolReader
{
    public Either<Error, Option<GeneSetIdentifier>> GetGenesetReference(GeneSetIdentifier geneSetId);

    public Either<Error, string> ReadGeneContent(GeneType geneType, GeneIdentifier geneId);
}