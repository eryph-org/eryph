using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualMachineNetworkAdapter
{
    [Key] public string Id { get; set; }

    public string Name { get; set; }

    public string SwitchName { get; set; }

    public string MacAddress { get; set; }
}