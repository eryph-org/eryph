using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecification
{
    public required string Id { get; set; }
    
    public required string Name { get; set; }

    public required string Architecture { get; set; }

    public required Project Project { get; set; }
}
