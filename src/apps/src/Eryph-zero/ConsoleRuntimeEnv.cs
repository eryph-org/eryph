using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dbosoft.OVN;
using Eryph.VmManagement;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Eryph.Runtime.Zero;

public class ConsoleRuntimeEnv(
    IAnsiConsole ansiConsole,
    ILoggerFactory loggerFactory,
    IPowershellEngine powershellEngine,
    ISystemEnvironment systemEnvironment,
    CancellationTokenSource tokenSource)
{
    public IAnsiConsole AnsiConsole => ansiConsole;

    public ILoggerFactory LoggerFactory => loggerFactory;

    public IPowershellEngine PowershellEngine => powershellEngine;

    public ISystemEnvironment SystemEnvironment => systemEnvironment;

    public CancellationTokenSource TokenSource => tokenSource;

    public CancellationToken Token => tokenSource.Token;
}
