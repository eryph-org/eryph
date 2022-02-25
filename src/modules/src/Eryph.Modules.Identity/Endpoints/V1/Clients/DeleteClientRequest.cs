using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

public class DeleteClientRequest
{
    [FromRoute(Name = "id")] public string Id { get; set; }
}