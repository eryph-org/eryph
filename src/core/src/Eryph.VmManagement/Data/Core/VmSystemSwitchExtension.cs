using Eryph.ConfigModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement.Data.Core
{
    public class VmSystemSwitchExtension
    {
        public string Id { get; init; }

        public string Name { get; init; }

        public string Vendor { get; set; }

        public string Version { get; set; }
    }
}
