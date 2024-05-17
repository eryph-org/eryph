using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement.Data.Core;

/// <summary>
/// Represents a network neighbor as returned by the
/// <c>>Get-NetNeighbor</c> cmdlet.
/// </summary>
public class CimNetNeighbor
{
    public string IpAddress { get; set; }
    
    public string LinkLayerAddress { get; set; }
}