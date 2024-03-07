using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement.Sys
{
    public interface HyperVHostSettingsIO
    {
        public HostSettings GetHostSettings();
    }

    public readonly struct LiveHyperVHostSettingsIO : HyperVHostSettingsIO
    {
        public static readonly HyperVHostSettingsIO Default = new LiveHyperVHostSettingsIO();

        public HostSettings GetHostSettings()
        {
            throw new NotImplementedException();
        }
    }
}
