using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Genetics;

public interface IGenePoolApiKeyStore
{
    public EitherAsync<Error, Option<GenePoolApiKey>> GetApiKey(string genePoolName);

    public EitherAsync<Error, HashMap<string, GenePoolApiKey>> GetApiKeys();

    public EitherAsync<Error, Unit> SaveApiKey(string genePoolName, GenePoolApiKey apiKey);

    public EitherAsync<Error, Unit> RemoveApiKey(string genePoolName);
}
