using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class CatletSpecification : Resource
{
    public CatletSpecification()
    {
        ResourceType = ResourceType.CatletSpecification;
    }

    public ICollection<CatletSpecificationVersion> Versions { get; set; } = null!;
}
