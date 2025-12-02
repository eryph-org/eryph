using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class CatletSpecificationInfo
{
    public required string SpecificationId { get; set; }

    public required string SpecificationVersionId { get; set; }
}
