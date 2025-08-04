using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.GenePool.Genetics;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;

namespace Eryph.Modules.GenePool.Inventory;

internal interface IGenePoolInventoryFactory
{
    IGenePoolInventory Create(string genePoolPath, ILocalGenePool genePool);
}

internal class GenePoolInventoryFactory(
    Container container)
    : IGenePoolInventoryFactory
{
    public IGenePoolInventory Create(
        string genePoolPath,
        ILocalGenePool genePool) =>
        ActivatorUtilities.CreateInstance<GenePoolInventory>(
            container, genePoolPath, genePool);
}
