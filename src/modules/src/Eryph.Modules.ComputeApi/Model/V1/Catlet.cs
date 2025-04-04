﻿using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;
using CatletStatus = Eryph.StateDb.Model.CatletStatus;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class Catlet
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    /// <summary>
    /// The ID of the corresponding Hyper-V virtual machine.
    /// </summary>
    public required string VmId { get; set; }

    public required Project Project { get; set; }

    public required CatletStatus Status { get; set; }

    public IReadOnlyList<CatletNetwork>? Networks { get; set; }

    public IReadOnlyList<CatletNetworkAdapter>? NetworkAdapters { get; set; }

    public IReadOnlyList<CatletDrive>? Drives { get; set; }
}
