using Eryph.Modules.Identity.Models.V1;
using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

public class UpdateClientRequest
{
    [FromRoute(Name = "id")] public string Id { get; set; }
    [FromBody] public Client Client { get; set; }
}