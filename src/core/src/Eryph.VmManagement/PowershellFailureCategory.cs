namespace Eryph.VmManagement;

/// <summary>
/// Encodes the category of a <see cref="PowershellFailure"/>.
/// </summary>
/// <remarks>
/// This class intentionally does not fully encode
/// <see cref="System.Management.Automation.ErrorCategory"/>.
/// See <see cref="PowershellEngine"/> for further information.
/// </remarks>
public enum PowershellFailureCategory
{
    Other,

    /// <summary>
    /// The requested entity was not found. This category is only used
    /// when a Cmdlet (e.g. <c>Get-VM</c>) cannot find the requested entity.
    /// </summary>
    ObjectNotFound,
    
    /// <summary>
    /// The pipeline has been aborted using <see cref="System.Threading.CancellationToken"/>.
    /// </summary>
    PipelineStopped,
}
