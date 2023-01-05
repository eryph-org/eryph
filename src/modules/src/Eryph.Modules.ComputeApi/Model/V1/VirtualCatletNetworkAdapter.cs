using System.ComponentModel.DataAnnotations;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class VirtualCatletNetworkAdapter
{

    public string Name { get; set; }
    
    public string MacAddress { get; set; }
}