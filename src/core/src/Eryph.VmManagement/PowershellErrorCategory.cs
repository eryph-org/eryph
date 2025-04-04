namespace Eryph.VmManagement;

/// <summary>
/// Encodes the category of a <see cref="PowershellError"/>.
/// </summary>
/// <remarks>
/// This enum encodes all error categories defined by
/// <see cref="System.Management.Automation.ErrorCategory"/>.
/// Additionally, it defines some custom categories. These categories
/// identify certain special cases.
/// </remarks>
public enum PowershellErrorCategory
{
    NotSpecified = 0,
    OpenError = 1,
    CloseError = 2,
    DeviceError = 3,
    DeadlockDetected = 4,
    InvalidArgument = 5,
    InvalidData = 6,
    InvalidOperation = 7,
    InvalidResult = 8,
    InvalidType = 9,
    MetadataError = 10,
    NotImplemented = 11,
    NotInstalled = 12,
    ObjectNotFound = 13,
    OperationStopped = 14,
    OperationTimeout = 15,
    SyntaxError = 16,
    ParserError = 17,
    PermissionDenied = 18,
    ResourceBusy = 19,
    ResourceExists = 20,
    ResourceUnavailable = 21,
    ReadError = 22,
    WriteError = 23,
    FromStdErr = 24,
    SecurityError = 25,
    ProtocolError = 26,
    ConnectionError = 27,
    AuthenticationError = 28,
    LimitsExceeded = 29,
    QuotaExceeded = 30,
    NotEnabled = 31,

    /// <summary>
    /// This custom category indicates that the Powershell pipeline
    /// has been stopped by the cancellation token.
    /// </summary>
    /// <remarks>
    /// Powershell uses the category <see cref="System.Management.Automation.ErrorCategory.OperationStopped"/>
    /// which is also used at least when a terminating error is thrown.
    /// </remarks>
    PipelineStopped = 0x40000000,

    /// <summary>
    /// This custom category indicates that a command was not found.
    /// </summary>
    /// <remarks>
    /// Powershell uses the category <see cref="System.Management.Automation.ErrorCategory.ObjectNotFound"/>
    /// which is also used when a Powershell command cannot find any results.
    /// </remarks>
    CommandNotFound = 0x40000001,
}
