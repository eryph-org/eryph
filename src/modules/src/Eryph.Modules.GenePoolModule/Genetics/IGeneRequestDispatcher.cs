using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Core.Genetics;

namespace Eryph.Modules.GenePool.Genetics;

public interface IGeneRequestDispatcher
{
    ValueTask NewGeneRequestTask(IOperationTaskMessage message, UniqueGeneIdentifier uniqueGeneId, GeneHash geneHash);
}
