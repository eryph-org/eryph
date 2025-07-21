using Eryph.Core.Sys;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.Genepool;

internal class GenepoolRuntimeEnv(ILoggerFactory loggerFactory)
{
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
