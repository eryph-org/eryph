using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model;

public class VirtualNetwork
{
    [Key] public string Id { get; set; }

    public string Name { get; set; }

    public string ProjectId { get; set; }
    public string ProjectName { get; set; }

    public string Environment { get; set; }

    public string TenantId { get; set; }

    public string ProviderName { get; set; }

    public string IpNetwork { get; set; }
}