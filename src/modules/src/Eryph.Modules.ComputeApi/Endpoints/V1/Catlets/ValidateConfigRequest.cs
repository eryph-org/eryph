using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class QuickValidateConfigRequest
{
    public required JsonElement Configuration { get; set; }
}
