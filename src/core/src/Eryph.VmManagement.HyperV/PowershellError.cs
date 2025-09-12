using System;
using System.Management.Automation;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public record PowershellError : Error
{
    internal PowershellError(
        string message,
        int code,
        Option<string> activity,
        PowershellErrorCategory category,
        Option<string> reason,
        Option<string> targetName,
        Option<string> targetType)
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

    public Option<string> Activity { get; }

    public PowershellErrorCategory Category { get; }

    public Option<string> Reason { get; }

    public Option<string> TargetName { get; }

    public Option<string> TargetType { get; }

    public override bool IsExceptional => false;

    public override bool IsExpected => true;

    public override bool Is<E>() => false;

    public override bool Is(Error error) =>
        error is PowershellError pe && pe.Category == Category && pe.Code == Code;

    public override ErrorException ToErrorException() =>
        new PowershellErrorException(Message, Code, Activity, Category, Reason, TargetName, TargetType);

    public static PowershellError New(RuntimeException runtimeException) =>
        new(Optional(runtimeException.ErrorRecord.ErrorDetails)
                .Bind(r => Optional(r.Message))
                .Filter(notEmpty)
                .IfNone(runtimeException.Message),
            runtimeException.HResult,
            Optional(runtimeException.ErrorRecord.CategoryInfo.Activity).Filter(notEmpty),
            ToPowershellErrorCategory(
                runtimeException.ErrorRecord.CategoryInfo.Category,
                runtimeException.ErrorRecord.FullyQualifiedErrorId),
            Optional(runtimeException.ErrorRecord.CategoryInfo.Reason).Filter(notEmpty),
            Optional(runtimeException.ErrorRecord.CategoryInfo.TargetName).Filter(notEmpty),
            Optional(runtimeException.ErrorRecord.CategoryInfo.TargetType).Filter(notEmpty));

    public static PowershellError New(ErrorRecord errorRecord) =>
        new(Optional(errorRecord.ErrorDetails)
                .Bind(r => Optional(r.Message))
                .Filter(notEmpty)
                .IfNone(errorRecord.Exception.Message),
            errorRecord.Exception.HResult,
            Optional(errorRecord.CategoryInfo.Activity).Filter(notEmpty),
            ToPowershellErrorCategory(errorRecord.CategoryInfo.Category, errorRecord.FullyQualifiedErrorId),
            Optional(errorRecord.CategoryInfo.Reason).Filter(notEmpty),
            Optional(errorRecord.CategoryInfo.TargetName).Filter(notEmpty),
            Optional(errorRecord.CategoryInfo.TargetType).Filter(notEmpty));

    private static PowershellErrorCategory ToPowershellErrorCategory(
        ErrorCategory category, string fullyQualifiedErrorId) =>
        fullyQualifiedErrorId switch
        {
            "CommandNotFoundException" => PowershellErrorCategory.CommandNotFound,
            "PipelineStopped" => PowershellErrorCategory.PipelineStopped,
            _ => category.ToPowershellErrorCategory()
        };
}
