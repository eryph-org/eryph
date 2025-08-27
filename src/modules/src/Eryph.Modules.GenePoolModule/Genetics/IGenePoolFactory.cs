using System.Collections.Generic;

namespace Eryph.Modules.GenePool.Genetics;

internal interface IGenePoolFactory
{
    IReadOnlyList<string> RemotePools { get; }
    
    IGenePool CreateNew(string name);
}
