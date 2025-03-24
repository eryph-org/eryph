using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices.JavaScript;
using System.Speech.Synthesis.TtsEngine;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public record PowershellError : Error
{
    internal PowershellError(
        string message,
        int code,
        string activity,
        int category,
        string reason,
        string targetName,
        string targetType)
    {
        Message = message;
        Code = code;

        Activity = activity;
        Category = category;
        Reason = reason;
        TargetName = targetName;
        TargetType = targetType;
    }

    public override string Message { get; }

    public override int Code { get; }

    public string Activity { get; }

    // TODO category should be enum
    public int Category { get; }

    public string Reason { get; }

    public string TargetName { get; }

    public string TargetType { get; }

    public override bool IsExceptional => false;

    public override bool IsExpected => true;

    public override bool Is<E>() => false;

    public override ErrorException ToErrorException()
    {
        throw new NotImplementedException();
    }

    public static PowershellError New(RuntimeException runtimeException) =>
        new(runtimeException.ErrorRecord.CategoryInfo.GetMessage(CultureInfo.InvariantCulture),
            runtimeException.HResult,
            runtimeException.ErrorRecord.CategoryInfo.Activity,
            (int)runtimeException.ErrorRecord.CategoryInfo.Category,
            runtimeException.ErrorRecord.CategoryInfo.Reason,
            runtimeException.ErrorRecord.CategoryInfo.TargetName,
            runtimeException.ErrorRecord.CategoryInfo.TargetType);

    public static PowershellError New(ErrorRecord errorRecord) =>
        new(errorRecord.CategoryInfo.GetMessage(CultureInfo.InvariantCulture),
            errorRecord.Exception.HResult,
            errorRecord.CategoryInfo.Activity,
            (int)errorRecord.CategoryInfo.Category,
            errorRecord.CategoryInfo.Reason,
            errorRecord.CategoryInfo.TargetName,
            errorRecord.CategoryInfo.TargetType);
}

public class PowershellErrorException(
    string message,
    int code,
    string activity,
    int category,
    string reason,
    string targetName,
    string targetType)
    : ErrorException(code)
{
    public override string Message { get; } = message;

    public override int Code { get; } = code;

    public string Activity { get; } = activity;

    // TODO category should be enum
    public int Category { get; } = category;

    public string Reason { get; } = reason;

    public string TargetName { get; } = targetName;

    public string TargetType { get; } = targetType;

    public override Option<ErrorException> Inner => Option<ErrorException>.None;

    public override bool IsExceptional => false;

    public override bool IsExpected => true;

    public override ErrorException Append(ErrorException error) =>
        error is ManyExceptions m
            ? new ManyExceptions(error.Cons(m.Errors))
            : new ManyExceptions(Seq(this, error));

    public override Error ToError() =>
        new PowershellError(Message, Code, Activity, Category, Reason, TargetName, TargetType);
}
