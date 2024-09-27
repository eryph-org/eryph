using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class NewVirtualDiskRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required string ProjectId { get; set; }

    public required string Name { get; set; }

    public required string Location { get; set; }

    public required int Size { get; set; }

    public string? Environment { get; set; }

    public string? Store { get; set; }
}
