using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Sys;
using Eryph.GenePool;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.Modules.GenePool.Inventory;

public interface IGenePoolInventory
{
    Aff<CancelRt, Seq<GeneData>> InventorizeGenePool();

    Aff<CancelRt, Seq<GeneData>> InventorizeGeneSet(
        GeneSetIdentifier geneSetId);
}
