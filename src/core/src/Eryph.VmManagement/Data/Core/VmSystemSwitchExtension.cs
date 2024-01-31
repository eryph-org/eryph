using Eryph.ConfigModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement.Data.Core
{
    /// <summary>
    /// Contains information about an installed Hyper-V switch extension which
    /// was retrieved via Powershell with <c>Get-VMSystemSwitchExtension</c>.
    /// </summary>
    public class VMSystemSwitchExtension
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public string Vendor { get; set; }

        public string Version { get; set; }
    }
}
