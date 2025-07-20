using System.Collections.Generic;

namespace Eryph.Modules.VmHostAgent.Genetics;

internal interface IGenePoolFactory
{
    IEnumerable<string> RemotePools { get; }
    IGenePool CreateNew(string name);
    ILocalGenePool CreateLocal(string genePoolPath);

}