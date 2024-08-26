using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel;
using Eryph.StateDb.Model;

namespace Eryph.StateDb.Specifications;

public class GeneSetSpecs
{
    public sealed class GetForInventory : Specification<GeneSet>, ISingleResultSpecification
    {
        public GetForInventory(GeneSetIdentifier geneSetId)
        {
            Query.Where(x => x.Organization == geneSetId.Organization.Value
                             && x.Name == geneSetId.GeneSet.Value
                             && x.Tag == geneSetId.Tag.Value)
                .Include(x => x.Genes);
        }
    }
}