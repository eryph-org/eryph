using System.Collections.Generic;
using System.Management.Automation;

namespace Eryph.VmManagement.Data.Full;

public class PowershellCommand
{

    /// <summary>Gets the name of the command.</summary>
    public string Name { get; init; }

    
    /// <summary>Return the parameters for this command.</summary>
    public virtual Dictionary<string, ParameterMetadata> Parameters { get; init; }



}