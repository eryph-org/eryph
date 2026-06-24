using System;
using System.Text.Json;
using Eryph.Modules.AspNetCore.ApiProvider.Model;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Catlets;

public class ExpandNewCatletConfigRequest : RequestBase
{
    public Guid? CorrelationId { get; set; }

    public required JsonElement Configuration { get; set; }

    public bool? ShowSecrets { get; set; }
}
