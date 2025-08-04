using System.Collections.Generic;

namespace Eryph.Modules.GenePool.Genetics;

internal interface IGenePoolFactory
{
    IEnumerable<string> RemotePools { get; }
    IGenePool CreateNew(string name);
    ILocalGenePool CreateLocal(string genePoolPath);

}