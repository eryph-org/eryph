using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Runtime.Uninstaller
{
    public enum UninstallReason
    {
        Other = 0,
        NotNeededAnymore = 1,
        TechnicalIssues = 2,
        WillReinstallLater = 3,
    }
}
