using Eryph.Resources;

namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class OperationResource
{
    public required string Id { get; set; }

    public required string ResourceId { get; set; }
    
    public required ResourceType ResourceType { get; set; }
}
