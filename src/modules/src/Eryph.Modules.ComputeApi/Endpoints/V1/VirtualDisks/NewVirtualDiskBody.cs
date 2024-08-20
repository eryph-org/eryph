using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.VirtualDisks;

public class NewVirtualDiskBody
{
    public Guid? CorrelationId { get; set; }
    
    public required string Name { get; set; }

    public string Environment { get; set; }

    public string Store { get; set; }

    public string Location { get; set; }
}
