using Vanara.PInvoke;

namespace Eryph.Modules.VmHostAgent.Networks;

public class WindowsArpCache : IWindowsArpCache
{
    public IpHlpApi.MIB_IPNETROW[] GetIpNetTable()
    {
        return IpHlpApi.GetIpNetTable().table;

    }

    public void DeleteIpNetEntry(IpHlpApi.MIB_IPNETROW row)
    {
        IpHlpApi.DeleteIpNetEntry(row);
    }
}