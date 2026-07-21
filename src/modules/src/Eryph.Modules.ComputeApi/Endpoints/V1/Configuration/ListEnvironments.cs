using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Environment = Eryph.Modules.ComputeApi.Model.V1.Environment;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Configuration;

[Route("v{version:apiVersion}")]
public class ListEnvironments(IEnvironmentsConfigProvider environmentsConfigProvider)
    : EndpointBaseAsync.WithoutRequest.WithActionResult<ListResponse<Environment>>
{
    [Authorize(Policy = "compute:basic")]
    [HttpGet("environments")]
    [SwaggerOperation(
            Summary = "List all environments",
            Description = "List the environments of the deployment together with the site which realizes "
                          + "each, as an option list for configuration input.",
            OperationId = "Environments_List",
            Tags = ["Configuration"])]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ListResponse<Environment>), "application/json")]
    public override Task<ActionResult<ListResponse<Environment>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var config = environmentsConfigProvider.Current;

        // The default environment is reserved and never authored, so it is absent from the distributed
        // catalog; add it here (mapped to the default site) so the option list is complete for clients.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EryphConstants.DefaultEnvironmentName };
        var environments = new List<Environment>
        {
            new() { Name = EryphConstants.DefaultEnvironmentName, Site = EryphConstants.DefaultSiteName },
        };

        foreach (var environment in config.Environments ?? [])
        {
            if (string.IsNullOrWhiteSpace(environment.Name) || !seen.Add(environment.Name))
                continue;

            // A distributed payload always names the site (filled with the default at authoring time),
            // but guard the empty case so a malformed value still yields a usable option.
            environments.Add(new Environment
            {
                Name = environment.Name,
                Site = string.IsNullOrWhiteSpace(environment.Site)
                    ? EryphConstants.DefaultSiteName
                    : environment.Site,
            });
        }

        return Task.FromResult<ActionResult<ListResponse<Environment>>>(
            new ListResponse<Environment> { Value = environments });
    }
}
