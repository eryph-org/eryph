using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace Eryph.Runtime.Zero;

internal static class ZeroLogging
{
    public static Logger CreateLogger(IConfiguration configuration)
    {
        var consoleTemplate = new ExpressionTemplate(
            "[{@t:yyyy-MM-dd HH:mm:ss.fff} {@l:u3}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}{InnerError}",
            theme: TemplateTheme.Literate);

        return CreateLoggerBaseConfig(configuration)
            .WriteTo.Logger(c => c
                .MinimumLevel.Debug()
                .WriteTo.Console(consoleTemplate))
            .CreateLogger();
    }

    public static Logger CreateWarmupLogger(IConfiguration configuration)
    {
        var consoleTemplate = new ExpressionTemplate(
            "WARMUP [{@t:HH:mm:ss} {@l:u3}] {@m}\n{@x}{InnerError}",
            theme: null);
        
        return CreateLoggerBaseConfig(configuration)
            .WriteTo.Logger(c => c
                .MinimumLevel.Information()
                .Filter.ByIncludingOnly(e => e.Properties.IsWarmupProgress())
                .WriteTo.Console(consoleTemplate))
            .CreateLogger();
    }

    public static Logger CreateInstallLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.With<ErrorEnricher>()
            .WriteTo.Console(new ExpressionTemplate(
                "[{@t:HH:mm:ss} {@l:u3}] {@m}\n{Inspect(@x).Message}{InnerError}",
                theme: null))
            .CreateLogger();
    }

    private static LoggerConfiguration CreateLoggerBaseConfig(IConfiguration configuration)
    {
        var logFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "eryph", "zero", "logs");

        var fileTemplate = new ExpressionTemplate(
            "[{@t:yyyy-MM-dd HH:mm:ss.fff zzz} {@l:u3}] [{SourceContext}] {#if ovsLogLevel is not null}[OVS:{controlFile}:{ovsSender}:{ovsLogLevel}] {#end}{@m}\n{@x}{InnerError}");

        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.With<ErrorEnricher>()
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
    }
}
