﻿using System;
using System.Collections.Generic;
using Eryph.Resources;

namespace Eryph.StateDb.Model;

public class CatletFarm : Resource
{
    public CatletFarm()
    {
        ResourceType = ResourceType.CatletFarm;
    }

    public ICollection<Catlet> Catlets { get; set; } = null!;

    public required string HardwareId { get; set; }

    public DateTimeOffset LastInventory { get; set; }
}
