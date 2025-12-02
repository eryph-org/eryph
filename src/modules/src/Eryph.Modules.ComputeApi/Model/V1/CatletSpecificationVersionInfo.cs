using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationVersionInfo
{
    public required string Id { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    public string? Comment { get; set; }
}
