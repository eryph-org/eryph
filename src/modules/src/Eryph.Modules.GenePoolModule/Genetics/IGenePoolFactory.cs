using System.Collections.Generic;

namespace Eryph.Modules.GenePool.Genetics;

internal interface IGenePoolFactory
{
    IReadOnlyList<string> GetRemotePools();
    
    IGenePool CreateNew(string name);
}
