using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.VmManagement;

internal static class PowershellErrorCategoryExtensions
{
    public static PowershellErrorCategory ToPowershellErrorCategory(
        this ErrorCategory errorCategory) =>
        errorCategory switch
        {
            ErrorCategory.NotSpecified => PowershellErrorCategory.NotSpecified,
            ErrorCategory.OpenError => PowershellErrorCategory.OpenError,
            ErrorCategory.CloseError => PowershellErrorCategory.CloseError,
            ErrorCategory.DeviceError => PowershellErrorCategory.DeviceError,
            ErrorCategory.DeadlockDetected => PowershellErrorCategory.DeadlockDetected,
            ErrorCategory.InvalidArgument => PowershellErrorCategory.InvalidArgument,
            ErrorCategory.InvalidData => PowershellErrorCategory.InvalidData,
            ErrorCategory.InvalidOperation => PowershellErrorCategory.InvalidOperation,
            ErrorCategory.InvalidResult => PowershellErrorCategory.InvalidResult,
            ErrorCategory.InvalidType => PowershellErrorCategory.InvalidType,
            ErrorCategory.MetadataError => PowershellErrorCategory.MetadataError,
            ErrorCategory.NotImplemented => PowershellErrorCategory.NotImplemented,
            ErrorCategory.NotInstalled => PowershellErrorCategory.NotInstalled,
            ErrorCategory.ObjectNotFound => PowershellErrorCategory.ObjectNotFound,
            ErrorCategory.OperationStopped => PowershellErrorCategory.OperationStopped,
            ErrorCategory.OperationTimeout => PowershellErrorCategory.OperationTimeout,
            ErrorCategory.SyntaxError => PowershellErrorCategory.SyntaxError,
            ErrorCategory.ParserError => PowershellErrorCategory.ParserError,
            ErrorCategory.PermissionDenied => PowershellErrorCategory.PermissionDenied,
            ErrorCategory.ResourceBusy => PowershellErrorCategory.ResourceBusy,
            ErrorCategory.ResourceExists => PowershellErrorCategory.ResourceExists,
            ErrorCategory.ResourceUnavailable => PowershellErrorCategory.ResourceUnavailable,
            ErrorCategory.ReadError => PowershellErrorCategory.ReadError,
            ErrorCategory.WriteError => PowershellErrorCategory.WriteError,
            ErrorCategory.FromStdErr => PowershellErrorCategory.FromStdErr,
            ErrorCategory.SecurityError => PowershellErrorCategory.SecurityError,
            ErrorCategory.ProtocolError => PowershellErrorCategory.ProtocolError,
            ErrorCategory.ConnectionError => PowershellErrorCategory.ConnectionError,
            ErrorCategory.AuthenticationError => PowershellErrorCategory.AuthenticationError,
            ErrorCategory.LimitsExceeded => PowershellErrorCategory.LimitsExceeded,
            ErrorCategory.QuotaExceeded => PowershellErrorCategory.QuotaExceeded,
            ErrorCategory.NotEnabled => PowershellErrorCategory.NotEnabled,
            _ => throw new ArgumentException(
                $"The Powershell error category {errorCategory} is not supported.",
                nameof(errorCategory))
        };
}
