using Microsoft.AspNetCore.Mvc;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model;

public class SingleEntityRequest : RequestBase
{
    [FromRoute(Name = "id")] public required string Id { get; set; }
}
