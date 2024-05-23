using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Eryph.Runtime.Zero;

internal static class ZeroLogging
{
    public static Logger CreateLogger(IConfiguration configuration)
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "zero", "logs");

        var consoleTemplate = new ExpressionTemplate(
            "[{@t:yyyy-MM-dd HH:mm:ss.fff} {@l:u3}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}",
            theme: TemplateTheme.Literate);
        var fileTemplate = new ExpressionTemplate(
            "[{@t:yyyy-MM-dd HH:mm:ss.fff zzz} {@l:u3}] [{SourceContext}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}");

        var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .WriteTo.Logger(c => c
                .MinimumLevel.Debug()
                .WriteTo.Console(consoleTemplate))
            .WriteTo.Logger(c => c
                .MinimumLevel.Error()
                .WriteTo.EventLog(source: "eryph-zero", logName: "Application"))
            .WriteTo.Logger(c => c
                .WriteTo.File(
                    fileTemplate,
                    Path.Combine(logFolder, "eryph-zero-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10,
                    retainedFileTimeLimit: TimeSpan.FromDays(30)));

        return loggerConfiguration.CreateLogger();
    }
}
