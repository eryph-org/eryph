using System.Collections.Generic;
using Serilog;
using Serilog.Events;

namespace Eryph.Runtime.Zero;

internal static class LoggerExtensions
{
    public static ILogger ForWarmupProgress(
        this ILogger logger) =>
        logger.ForContext(ModuleCore.Logging.LoggerExtensions.IsWarmupProgress, true);

    public static bool IsWarmupProgress(
        this IReadOnlyDictionary<string, LogEventPropertyValue> properties)
    {
        if (!properties.TryGetValue(ModuleCore.Logging.LoggerExtensions.IsWarmupProgress, out var propertyValue))
            return false;

        return propertyValue is ScalarValue { Value: true };
    }
}
