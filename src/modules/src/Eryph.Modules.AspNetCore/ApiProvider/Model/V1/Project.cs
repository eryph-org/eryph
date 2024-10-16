namespace Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

public class Project
{
    public required string Id { get; set; }

    public required string Name { get; set; }
    
    public required string TenantId { get; set; }
}
