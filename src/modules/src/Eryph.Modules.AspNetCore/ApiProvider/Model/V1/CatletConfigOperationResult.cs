using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class CatletConfigOperationResult : OperationResult
{
    public JsonElement Configuration { get; set; }
}
