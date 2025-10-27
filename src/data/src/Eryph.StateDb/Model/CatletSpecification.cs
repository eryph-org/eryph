using System.Collections.Generic;
using Eryph.Core.Genetics;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class CatletSpecification : Resource
{
    public CatletSpecification()
    {
        ResourceType = ResourceType.CatletSpecification;
    }

    public required ISet<Architecture> Architectures { get; set; }

    public ICollection<CatletSpecificationVersion> Versions { get; set; } = null!;
}
