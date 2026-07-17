using System.Collections.Generic;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.CatletSpecifications;

public class DeployCatletSpecificationRequest : SingleEntityRequest
{
    [FromRoute(Name = "specification_id")] public required string SpecificationId { get; set; }

    [FromBody] public required DeployCatletSpecificationRequestBody Body { get; set; }
}

public class DeployCatletSpecificationRequestBody
{
    public string? Architecture { get; set; }

    /// <summary>
    /// The environment to deploy into; the default environment when omitted. A specification is
    /// project level and can be deployed into several environments, at most once into each.
    /// </summary>
    public string? Environment { get; set; }

    public bool? Redeploy { get; set; }

    public required IReadOnlyDictionary<string, string> Variables { get; set; }
}
