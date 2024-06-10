using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.Identity.Endpoints.V1.Clients;

public class NewClientKeyRequest
{
    [FromRoute(Name = "id")] public required string Id { get; set; }

    [FromBody] 
    public required NewClientKeyRequestBody Body { get; set; }
}

public class NewClientKeyRequestBody
{
    public bool? SharedSecret { get; set; }
}
