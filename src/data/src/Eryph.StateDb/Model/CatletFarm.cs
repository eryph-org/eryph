using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class CatletFarm : Resource, ISiteBound
{
    public CatletFarm()
    {
        ResourceType = ResourceType.CatletFarm;
    }

    public required Guid SiteId { get; set; }

    public Site Site { get; set; } = null!;

    public ICollection<Catlet> Catlets { get; set; } = null!;


    public DateTimeOffset LastInventory { get; set; }
}
