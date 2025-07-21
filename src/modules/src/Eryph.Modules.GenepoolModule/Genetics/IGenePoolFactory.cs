using System.Collections.Generic;

namespace Eryph.Modules.Genepool.Genetics;

internal interface IGenePoolFactory
{
    IEnumerable<string> RemotePools { get; }
    IGenePool CreateNew(string name);
    ILocalGenePool CreateLocal(string genePoolPath);

}