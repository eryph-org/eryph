using System.Collections.Generic;
using Eryph.StateDb.Model;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class Catlet
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required CatletStatus Status { get; set; }

    public IEnumerable<CatletNetwork>? Networks { get; set; }

    public IEnumerable<CatletNetworkAdapter>? NetworkAdapters { get; set; }

    public IEnumerable<CatletDrive>? Drives { get; set; }
}
