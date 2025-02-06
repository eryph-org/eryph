using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class ValidationIssue
{
    /// <summary>
    /// The JSON path which identifies the member which has the issue.
    /// Can be <see langword="null"/> when the issue is not related to
    /// a specific member.
    /// </summary>
    public string? Member { get; set; }
    
    /// <summary>
    /// The details of the issue.
    /// </summary>
    public required string Message { get; set; }
}
