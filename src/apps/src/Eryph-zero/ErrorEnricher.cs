using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.Core;
using LanguageExt.Common;
using LanguageExt.UnsafeValueAccess;
using Serilog.Core;
using Serilog.Events;

namespace Eryph.Runtime.Zero;

/// <summary>
/// This <see cref="ILogEventEnricher"/> enriches the log event with
/// the details of an <see cref="Error"/> when an <see cref="ErrorException"/>
/// is logged. Without this enricher only the top-level error message would
/// be included in the log.
/// </summary>
public class ErrorEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        if (logEvent.Exception is ErrorException { Inner.IsSome: true } eex)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(
                "InnerError",
                ErrorUtils.PrintError(eex.Inner.ValueUnsafe().ToError())));
        }
    }
}
