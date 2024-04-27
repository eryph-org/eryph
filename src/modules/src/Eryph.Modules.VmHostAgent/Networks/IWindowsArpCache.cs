using System.Collections;
using Vanara.PInvoke;

namespace Eryph.Modules.VmHostAgent.Networks;

public interface IWindowsArpCache
{
    IpHlpApi.MIB_IPNETROW[] GetIpNetTable();
    void DeleteIpNetEntry(IpHlpApi.MIB_IPNETROW row);
}