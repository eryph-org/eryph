using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class CatletFarm : Resource
{
    public CatletFarm()
    {
        ResourceType = ResourceType.CatletFarm;
    }

    public virtual ICollection<Catlet> Catlets { get; set; } = null!;

    public string HardwareId { get; set; } = "";
}
