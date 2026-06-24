using Microsoft.Extensions.Logging;

namespace Eryph.Modules.GenePool;

internal class GenePoolRuntimeEnv(ILoggerFactory loggerFactory)
{
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
