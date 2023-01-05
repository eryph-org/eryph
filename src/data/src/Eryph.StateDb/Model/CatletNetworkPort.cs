﻿using System;
using JetBrains.Annotations;

namespace Eryph.StateDb.Model;

public class CatletNetworkPort : VirtualNetworkPort
{


    public Guid? CatletId { get; set; }
    [CanBeNull] public Catlet Catlet { get; set; }

}