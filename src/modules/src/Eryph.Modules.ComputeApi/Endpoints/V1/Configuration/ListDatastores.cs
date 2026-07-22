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
public class ListDatastores(IStorageConfigProvider storageConfigProvider)
    : EndpointBaseAsync.WithoutRequest.WithActionResult<ListResponse<Datastore>>
{
    [Authorize(Policy = "compute:basic")]
    [HttpGet("config/datastores")]
    [SwaggerOperation(
            Summary = "List all datastores",
            Description = "List the datastore names of the deployment's storage vocabulary as an option "
                          + "list for configuration input. Paths are agent-local and not included.",
            OperationId = "Config_ListDatastores",
            Tags = ["Config"])]
    [SwaggerResponse(StatusCodes.Status200OK, "Success", typeof(ListResponse<Datastore>), "application/json")]
    public override Task<ActionResult<ListResponse<Datastore>>> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var config = storageConfigProvider.Current;

        // The default datastore is always available; add it first so the option list is complete even
        // before any storage config is distributed. Names are deduplicated case-insensitively.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { EryphConstants.DefaultDataStoreName };
        var datastores = new List<Datastore> { new() { Name = EryphConstants.DefaultDataStoreName } };

        void Add(string? name)
        {
            if (!string.IsNullOrWhiteSpace(name) && seen.Add(name))
                datastores.Add(new Datastore { Name = name });
        }

        // Union the global vocabulary with the per-environment datastores, so every selectable
        // datastore name appears once regardless of the scope it is declared at.
        foreach (var datastore in config.Datastores ?? [])
            Add(datastore.Name);

        foreach (var environment in config.Environments ?? [])
        foreach (var datastore in environment.Datastores ?? [])
            Add(datastore.Name);

        return Task.FromResult<ActionResult<ListResponse<Datastore>>>(
            new ListResponse<Datastore> { Value = datastores });
    }
}
