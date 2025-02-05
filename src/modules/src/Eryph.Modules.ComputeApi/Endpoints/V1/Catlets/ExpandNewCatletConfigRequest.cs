using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ExpandNewCatletConfigRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }
}
