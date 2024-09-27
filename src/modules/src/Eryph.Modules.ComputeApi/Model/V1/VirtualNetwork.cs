namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualNetwork
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required string ProjectId { get; set; }

    public required string ProjectName { get; set; }

    public required string Environment { get; set; }

    public required string TenantId { get; set; }

    public required string ProviderName { get; set; }

    public required string IpNetwork { get; set; }
}
