using Eryph.Modules.AspNetCore.ApiProvider.Model.V1;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualNetwork
{
    public required string Id { get; set; }

    public required string Name { get; set; }

    public required Project Project { get; set; }

    public required string Environment { get; set; }

    public required string ProviderName { get; set; }

    public string? IpNetwork { get; set; }
}
