using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Eryph.ModuleCore.Logging;

public static class LoggerExtensions
{
    public static readonly string IsWarmupProgress = "IsWarmupProgress";

    public static IDisposable? BeginWarmupProgressScope(this ILogger logger) =>
        logger.BeginScope(new Dictionary<string, object>
        {
            [IsWarmupProgress] = true,
        });
}
