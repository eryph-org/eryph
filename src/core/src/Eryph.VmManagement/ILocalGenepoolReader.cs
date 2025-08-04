using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement;

public interface ILocalGenePoolReader
{
    public EitherAsync<Error, Option<GeneSetIdentifier>> GetGenesetReference(
        GeneSetIdentifier geneSetId);

    public EitherAsync<Error, string> ReadGeneContent(
        UniqueGeneIdentifier uniqueGeneId);
}
