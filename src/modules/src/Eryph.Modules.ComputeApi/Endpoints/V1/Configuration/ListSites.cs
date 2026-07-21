using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.ApiEndpoints;
using Eryph.Core;
using Eryph.Modules.AspNetCore.ApiProvider.Model;
using Eryph.Modules.ComputeApi.Configuration;
using Eryph.Modules.ComputeApi.Model.V1;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Eryph.Modules.ComputeApi.Endpoints.V1.Configuration;

[Route("v{version:apiVersion}")]
public class ListSites(IEnvironmentsConfigProvider environmentsConfigProvider)
    : EndpointBaseAsync.WithoutRequest.WithActionResult<ListResponse<Site>>
{
    [Authorize(Policy = "compute:basic")]
    [HttpGet("sites")]
    [SwaggerOperation(
            Summary = "List all sites",
            Description = "List the sites of the deployment as an option list for configuration input.",
            OperationId = "Sites_List",
            Tags = ["Configuration"])]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ListResponse<Site>), "application/json")]
    public override Task<ActionResult<ListResponse<Site>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var config = environmentsConfigProvider.Current;

        // The default site is reserved and never authored, so it is absent from the distributed
        // catalog; add it here so the option list is complete for clients. Names are deduplicated
        // case-insensitively (the catalog is already lower-cased, but the default guard must not
        // depend on that).
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EryphConstants.DefaultSiteName };
        var sites = new List<Site> { new() { Name = EryphConstants.DefaultSiteName } };

        foreach (var site in config.Sites ?? [])
        {
            if (!string.IsNullOrWhiteSpace(site.Name) && seen.Add(site.Name))
                sites.Add(new Site { Name = site.Name });
        }

        return Task.FromResult<ActionResult<ListResponse<Site>>>(
            new ListResponse<Site> { Value = sites });
    }
}
