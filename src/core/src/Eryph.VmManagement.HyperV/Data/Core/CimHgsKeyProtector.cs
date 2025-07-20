using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Represent a HGS key protector as returned by the Cmdlets
/// <c>ConvertTo-HgsKeyProtector</c> or <c>New-HgsKeyProtector</c>.
/// </summary>
public class CimHgsKeyProtector
{
    public CimHgsGuardian Owner { get; init; }

    public byte[] RawData { get; init; }
}
