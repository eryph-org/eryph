using Dbosoft.Functional;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;
using Spectre.Console;
using Spectre.Console.Rendering;

using static LanguageExt.Prelude;

namespace Eryph.AnsiConsole.Sys;

public static class AnsiConsole<RT> where RT : struct, HasAnsiConsole<RT>
{
    public static Aff<RT, bool> confirm(string prompt, bool defaultValue) =>
        from cancelToken in cancelToken<RT>()
        let consolePrompt = new ConfirmationPrompt(prompt)
        {
            DefaultValue = defaultValue,
        }
        from result in default(RT).AnsiConsoleEff.MapAsync(
            async ac => await consolePrompt.ShowAsync(ac.AnsiConsole, cancelToken))
        select result;

    public static Aff<RT, T> prompt<T>(
        string prompt,
        Func<string, Validation<Error, T>> validate,
        Option<string> defaultValue = default) =>
        from cancelToken in cancelToken<RT>()
        // The default value is ignored in case it does not pass validation.
        let validDefaultValue = from v in defaultValue
                                from _ in validate(v).ToOption()
                                select v
        let consolePrompt = new TextPrompt<string>(prompt)
            .Validate(v => validate(v).Match(
                Succ: _ => ValidationResult.Success(),
                Fail: errors => ValidationResult.Error(
                    $"[red]{Markup.Escape(Error.Many(errors).Print())}[/]")))
        let consolePromptWithDefault = validDefaultValue.Match(
            Some: consolePrompt.DefaultValue,
            None: () => consolePrompt)
        from result in default(RT).AnsiConsoleEff.MapAsync(
            async ac => await consolePromptWithDefault.ShowAsync(ac.AnsiConsole, cancelToken))
        // We validate again as the validate function also performs the parsing.
        // The validation should always be successful. Otherwise, the prompt would
        // not have succeeded.
        from validResult in validate(result)
            .ToAff(errors => Error.New("Final validation of the user input failed.", Error.Many(errors)))
        select validResult;

    public static Eff<RT, Unit> markupLine(string text) =>
        default(RT).AnsiConsoleEff.Map(fun((AnsiConsoleIO io) => io.AnsiConsole.MarkupLine(text)));

    public static Eff<RT, Unit> writeLine(string text) =>
        default(RT).AnsiConsoleEff.Map(fun((AnsiConsoleIO io) => io.AnsiConsole.WriteLine(text)));

    public static Eff<RT, Unit> write(IRenderable renderable) =>
        default(RT).AnsiConsoleEff.Map(fun((AnsiConsoleIO io) => io.AnsiConsole.Write(renderable)));

    public static Eff<RT, Eff<Unit>> startSpinner(string text) =>
        from _ in SuccessEff(unit)
        let tcs = new TaskCompletionSource()
        let task = tcs.Task
        from __ in fork(
            from ct in cancelToken<RT>()
            from _ in default(RT).AnsiConsoleEff.MapAsync(
                async ac => await ac.AnsiConsole.Status().StartAsync(text, async _ =>
                {
                    await task.WaitAsync(ct);
                    return unit;
                }))
            select unit)
        select Eff(fun(tcs.SetResult));

    /// <summary>
    /// Renders a progress indicator for a single task with the name
    /// <paramref name="taskName"/>. The maximum value for the progress
    /// is set to <c>1</c>.
    /// </summary>
    public static Aff<RT, T> withProgress<T>(
        string taskName,
        Func<Func<double, Eff<Unit>>, Aff<RT, T>> aff) =>
        from ansiConsole in default(RT).AnsiConsoleEff
        from result in AffMaybe<RT, T>(async rt =>
        {
            return await ansiConsole.AnsiConsole.Progress().StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask(taskName, autoStart: true, maxValue: 1d);

                Eff<Unit> ReportProgress(double value) => Eff(() =>
                {
                    progressTask.Value = value;
                    return unit;
                });

                return await aff(ReportProgress).Run(rt);
            });
        })
        select result;

    public static Aff<RT, T> withSpinner<T>(
        string text, 
        Aff<RT, T> aff) =>
        from ansiConsole in default(RT).AnsiConsoleEff
        from result in AffMaybe<RT, T>(async rt =>
        {
            return await ansiConsole.AnsiConsole.Status().StartAsync(
                text,
                async ctx => await aff.Run(rt));
        })
        select result;
}
