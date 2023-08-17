using System.Threading.Tasks;
using Dbosoft.Rebus.Operations;
using Eryph.Resources;

namespace Eryph.Modules.VmHostAgent.Genetics;

public interface IGeneRequestDispatcher
{
    ValueTask NewGeneRequestTask(IOperationTaskMessage message, GeneType geneType, string geneName);
    ValueTask NewGenomeRequestTask(IOperationTaskMessage message, string genesetName);

}