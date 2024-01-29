using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

namespace Eryph.VmManagement.Data.Core
{
    /// <summary>
    /// Contains information about an installed driver package which
    /// was retrieved via Powershell with <c>Get-WindowsDriver</c>.
    /// </summary>
    public class DismDriverInfo
    {
        public string Driver { get; init; }

        public string OriginalFileName { get; init; }

        public string ProviderName { get; init; }

        public uint MajorVersion { get; init; }
        
        public uint MinorVersion { get; init; }

        public uint Build { get; init; }
        
        public uint Revision { get; init; }

        public string Version => $"{MajorVersion}.{MinorVersion}.{Build}.{Revision}";
    }
}
