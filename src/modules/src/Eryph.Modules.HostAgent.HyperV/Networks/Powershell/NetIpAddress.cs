namespace Eryph.Modules.HostAgent.Networks.Powershell;

public class NetIpAddress
{
    public string? IPAddress { get; private set; }
    public byte PrefixLength { get; private set; }
}