namespace Haipa.VmManagement.Data
{
    public interface IVMNetworkAdapterCore
    {
        string Id { get; }
        string Name { get;  }
        string MacAddress { get;  }
    }
}