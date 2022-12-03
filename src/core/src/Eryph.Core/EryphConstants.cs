using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Core
{
    public static class EryphConstants
    {
        public static readonly Guid DefaultTenantId = Guid.Parse("{C1813384-8ECB-4F17-B846-821EE515D19B}");
        public static readonly Guid DefaultProjectId = Guid.Parse("{4B4A3FCF-B5ED-4A9A-AB6E-03852752095E}");

        public const string OverlaySwitchName = "eryph_overlay";

    }
}
