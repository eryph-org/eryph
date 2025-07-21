using System;
using Eryph.Modules.VmHostAgent.Networks;
using LanguageExt;
using Microsoft.Extensions.Logging;

namespace Eryph.VmManagement.Sys;

public static class Logger<RT> where RT: struct, HasLogger<RT>
{

    private static Eff<RT,Unit> withLogger(string category, Action<ILogger> logAction)
    {
        return default(RT).Logger(category).Bind(l =>
        {
            logAction(l);
            return Prelude.unitEff;
        });
    }
    private static Eff<RT, Unit> withLogger<T>(Action<ILogger> logAction)
    {
        return default(RT).Logger<T>().Bind(l =>
        {
            logAction(l);
            return Prelude.unitEff;
        });
    }

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logDebug(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category,logger => logger.Log(LogLevel.Debug, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logDebug(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Debug, eventId, message, args));

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logDebug(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Debug, exception, message, args));

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logDebug(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Debug, message, args));

    //------------------------------------------TRACE------------------------------------------//

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logTrace(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Trace, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logTrace(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Trace, eventId, message, args));

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logTrace(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Trace, exception, message, args));

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logTrace(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Trace, message, args));

    //------------------------------------------INFORMATION------------------------------------------//

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logInformation(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Information, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logInformation(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Information, eventId, message, args));

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logInformation(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Information, exception, message, args));

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logInformation(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Information, message, args));

    //------------------------------------------WARNING------------------------------------------//

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logWarning(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Warning, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logWarning(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Warning, eventId, message, args));

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logWarning(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Warning, exception, message, args));

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logWarning(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Warning, message, args));

    //------------------------------------------ERROR------------------------------------------//

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logError(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Error, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logError(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Error, eventId, message, args));

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logError(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Error, exception, message, args));

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logError(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Error, message, args));

    //------------------------------------------CRITICAL------------------------------------------//

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logCritical(string category, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Critical, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(0, "Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logCritical(string category, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Critical, eventId, message, args));

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logCritical(string category, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Critical, exception, message, args));

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical("Processing request from {Address}", address)</example>
    public static Eff<RT,Unit> logCritical(string category, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(LogLevel.Critical, message, args));

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT,Unit> log(string category, LogLevel logLevel, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(logLevel, 0, null, message, args));

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT,Unit> log(string category, LogLevel logLevel, EventId eventId, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(logLevel, eventId, null, message, args));

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT,Unit> Log(string category, LogLevel logLevel, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(logLevel, 0, exception, message, args));

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT,Unit> log(string category, LogLevel logLevel, EventId eventId, Exception? exception, string? message, params object?[] args) => 
        withLogger(category, logger => logger.Log(logLevel, eventId, exception, message, args));


    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="category">log category</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logDebug<T>(EventId eventId, Exception? exception, string? message, params object?[] args) =>
        withLogger<T>(logger => logger.Log(LogLevel.Debug, eventId, exception, message, args));

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logDebug<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>( logger => logger.Log(LogLevel.Debug, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logDebug<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Debug, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a debug log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logDebug("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logDebug<T>(string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Debug, message, args));
    }

    //------------------------------------------TRACE------------------------------------------//

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logTrace<T>(EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Trace, eventId, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logTrace<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Trace, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logTrace<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Trace, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a trace log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logTrace("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logTrace<T>(string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Trace, message, args));
    }

    //------------------------------------------INFORMATION------------------------------------------//

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logInformation<T>(EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Information, eventId, exception, message, args));
    }

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logInformation<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Information, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logInformation<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Information, exception, message, args));
    }

    /// <summary>
    /// Formats and writes an informational log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logInformation("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logInformation<T>(string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Information, message, args));
    }

    //------------------------------------------WARNING------------------------------------------//

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logWarning<T>(EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Warning, eventId, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logWarning<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Warning, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logWarning<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Warning, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a warning log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logWarning("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logWarning<T>(string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Warning, message, args));
    }

    //------------------------------------------ERROR------------------------------------------//

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logError<T>(EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Error, eventId, exception, message, args));
    }

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logError<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Error, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logError<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Error, exception, message, args));
    }

    /// <summary>
    /// Formats and writes an error log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logError("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logError<T>(string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Error, message, args));
    }

    //------------------------------------------CRITICAL------------------------------------------//

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(0, exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logCritical<T>(EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Critical, eventId, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(0, "Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logCritical<T>(EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Critical, eventId, message, args));
    }

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical(exception, "Error while processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logCritical<T>(Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Critical, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a critical log message.
    /// </summary>
    /// <param name="message">Format string of the log message in message template format. Example: <c>"User {User} logged in from {Address}"</c></param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    /// <example>logger.logCritical("Processing request from {Address}", address)</example>
    public static Eff<RT, Unit> logCritical<T>( string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(LogLevel.Critical, message, args));
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT, Unit> Log<T>(LogLevel logLevel, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(logLevel, 0, null, message, args));
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT, Unit> Log<T>(LogLevel logLevel, EventId eventId, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(logLevel, eventId, null, message, args));
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT, Unit> Log<T>(LogLevel logLevel, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(logLevel, 0, exception, message, args));
    }

    /// <summary>
    /// Formats and writes a log message at the specified log level.
    /// </summary>
    /// <param name="logLevel">Entry will be written on this level.</param>
    /// <param name="eventId">The event id associated with the log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Format string of the log message.</param>
    /// <param name="args">An object array that contains zero or more objects to format.</param>
    public static Eff<RT, Unit> Log<T>( LogLevel logLevel, EventId eventId, Exception? exception, string? message, params object?[] args)
    {
        return withLogger<T>(logger => logger.Log(logLevel, eventId, exception, message,args));
    }



}