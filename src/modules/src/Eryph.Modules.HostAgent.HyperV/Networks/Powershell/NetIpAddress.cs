namespace Eryph.Modules.VmHostAgent.Networks.Powershell;

public class NetIpAddress
{
    public string? IPAddress { get; private set; }
    public byte PrefixLength { get; private set; }
}