using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.GenePool;
using Eryph.Modules.VmHostAgent.Genetics;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.VmHostAgent.Inventory;

public interface IGenePoolInventory
{
    EitherAsync<Error, Seq<GeneData>> InventorizeGenePool();

    EitherAsync<Error, Seq<GeneData>> InventoryGeneSet(
        GeneSetIdentifier geneSetId);
}
