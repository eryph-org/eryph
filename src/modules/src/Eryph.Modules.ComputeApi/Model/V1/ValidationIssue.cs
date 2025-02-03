using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.ComputeApi.Model.V1;

public class ValidationIssue
{
    /// <summary>
    /// The member of the configuration that has the issue. The value is a
    /// JSON path to the member. It can be <see langword="null"/> when the
    /// issue is not related to a specific member.
    /// </summary>
    public string? Member { get; set; }
    
    /// <summary>
    /// The details of the issue.
    /// </summary>
    public required string Message { get; set; }
}
