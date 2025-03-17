using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement;

internal static class PowershellFailureCategoryExtensions
{
    public static PowershellFailureCategory ToFailureCategory(
        this ErrorCategory errorCategory) =>
        errorCategory switch
        {
            ErrorCategory.NotSpecified => PowershellFailureCategory.NotSpecified,
            ErrorCategory.OpenError => PowershellFailureCategory.OpenError,
            ErrorCategory.CloseError => PowershellFailureCategory.CloseError,
            ErrorCategory.DeviceError => PowershellFailureCategory.DeviceError,
            ErrorCategory.DeadlockDetected => PowershellFailureCategory.DeadlockDetected,
            ErrorCategory.InvalidArgument => PowershellFailureCategory.InvalidArgument,
            ErrorCategory.InvalidData => PowershellFailureCategory.InvalidData,
            ErrorCategory.InvalidOperation => PowershellFailureCategory.InvalidOperation,
            ErrorCategory.InvalidResult => PowershellFailureCategory.InvalidResult,
            ErrorCategory.InvalidType => PowershellFailureCategory.InvalidType,
            ErrorCategory.MetadataError => PowershellFailureCategory.MetadataError,
            ErrorCategory.NotImplemented => PowershellFailureCategory.NotImplemented,
            ErrorCategory.NotInstalled => PowershellFailureCategory.NotInstalled,
            ErrorCategory.ObjectNotFound => PowershellFailureCategory.ObjectNotFound,
            ErrorCategory.OperationStopped => PowershellFailureCategory.OperationStopped,
            ErrorCategory.OperationTimeout => PowershellFailureCategory.OperationTimeout,
            ErrorCategory.SyntaxError => PowershellFailureCategory.SyntaxError,
            ErrorCategory.ParserError => PowershellFailureCategory.ParserError,
            ErrorCategory.PermissionDenied => PowershellFailureCategory.PermissionDenied,
            ErrorCategory.ResourceBusy => PowershellFailureCategory.ResourceBusy,
            ErrorCategory.ResourceExists => PowershellFailureCategory.ResourceExists,
            ErrorCategory.ResourceUnavailable => PowershellFailureCategory.ResourceUnavailable,
            ErrorCategory.ReadError => PowershellFailureCategory.ReadError,
            ErrorCategory.WriteError => PowershellFailureCategory.WriteError,
            ErrorCategory.FromStdErr => PowershellFailureCategory.FromStdErr,
            ErrorCategory.SecurityError => PowershellFailureCategory.SecurityError,
            ErrorCategory.ProtocolError => PowershellFailureCategory.ProtocolError,
            ErrorCategory.ConnectionError => PowershellFailureCategory.ConnectionError,
            ErrorCategory.AuthenticationError => PowershellFailureCategory.AuthenticationError,
            ErrorCategory.LimitsExceeded => PowershellFailureCategory.LimitsExceeded,
            ErrorCategory.QuotaExceeded => PowershellFailureCategory.QuotaExceeded,
            ErrorCategory.NotEnabled => PowershellFailureCategory.NotEnabled,
            _ => PowershellFailureCategory.NotSpecified
        };
}
