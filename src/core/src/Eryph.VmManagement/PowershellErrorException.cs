using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public class PowershellErrorException(
    string message,
    int code,
    Option<string> activity,
    PowershellErrorCategory category,
    Option<string> reason,
    Option<string> targetName,
    Option<string> targetType)
    : ErrorException(code)
{
    public override string Message { get; } = message;

    public override int Code { get; } = code;

    public Option<string> Activity { get; } = activity;

    public PowershellErrorCategory Category { get; } = category;

    public Option<string> Reason { get; } = reason;

    public Option<string> TargetName { get; } = targetName;

    public Option<string> TargetType { get; } = targetType;

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
