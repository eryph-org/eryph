using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

#nullable enable

public static partial class CatletConfigVariableSubstitutions
{
    public static Validation<ValidationIssue, CatletConfig> SubstituteVariables(
        CatletConfig config) =>
        from valuesMap in config.Variables
            .ToSeq()
            .Map(variableConfig =>
                from name in VariableName.NewValidation(variableConfig.Name)
                    .MapFail(e => new ValidationIssue(
                        CreateVariablePath(nameof(CatletConfig.Variables), variableConfig),
                        e.Message))
                let info = VariableInfo.FromConfig(variableConfig)
                from _ in ValidateCatletVariableInfo(info,
                    CreateVariablePath(nameof(CatletConfig.Variables), variableConfig))
                select (name, info))
            .Sequence()
            .Map(values => values.ToHashMap())
        from substitutedFodder in config.Fodder.ToSeq()
            .Map(fodderConfig => SubstituteVariables(
                fodderConfig,
                CreateFodderPath(nameof(CatletConfig.Fodder), fodderConfig),
                valuesMap))
            .Sequence()
        select config.CloneWith(c =>
        {
            c.Fodder = substitutedFodder.ToArray();
        });

    private static Validation<ValidationIssue, FodderConfig> SubstituteVariables(
        FodderConfig config,
        string path,
        HashMap<VariableName, VariableInfo> catletVariables) =>
        from combinedVariables in config.Variables.ToSeq()
            .Map(variableConfig => SubstituteVariables(
                config,
                variableConfig,
                CreateVariablePath(JoinPath(path, nameof(FodderConfig.Variables)), variableConfig),
                catletVariables))
            .Sequence()
            .Map(s => s.ToHashMap().Append(catletVariables))
        from result in SubstituteVariables(config.Content ?? "", combinedVariables)
            .MapFail(issue => ToValidationIssue(issue, config, JoinPath(path, nameof(FodderConfig.Content))))
        select config.CloneWith(c =>
        {
            c.Content = result.Value;
            c.Secret = result.Secret;
        });

    private static Validation<ValidationIssue, (VariableName Name, VariableInfo Info)> SubstituteVariables(
        FodderConfig fodderConfig,
        VariableConfig variableConfig,
        string path,
        HashMap<VariableName, VariableInfo> catletVariables) =>
        from name in VariableName.NewValidation(variableConfig.Name)
            .MapFail(e => new ValidationIssue(JoinPath(path, nameof(VariableConfig.Name)), e.Message))
        from t in SubstituteVariables(variableConfig.Value ?? "", catletVariables)
            .MapFail(issue => ToValidationIssue(issue, fodderConfig, JoinPath(path, nameof(VariableConfig.Value))))
        let info = VariableInfo.FromConfig(variableConfig)
        let substitutedInfo = info with
        {
            Value = t.Value,
            Secret = info.Secret || t.Secret,
        }
        from _ in ValidateFodderVariableInfo(fodderConfig, substitutedInfo, path)
        select (name, substitutedInfo);

    private static Validation<VariableReferenceIssue, (string Value, bool Secret)> SubstituteVariables(
        string input,
        HashMap<VariableName, VariableInfo> values)
    {
        var isSecret = false;
        var errors = new List<VariableReferenceIssue>();
        var result = VariableReferenceRegex().Replace(input, match =>
            MatchVariable(match, values).Match(
                Succ: t =>
                {
                    isSecret |= t.Secret;
                    return t.Value;
                },
                Fail: error =>
                {
                    errors.AddRange(error);
                    return match.Value;
                }));

        return errors.Count > 0 ? errors.ToSeq() : (result, isSecret);
    }

    private static Validation<VariableReferenceIssue, (string Value, bool Secret)> MatchVariable(
        Match match,
        HashMap<VariableName, VariableInfo> variables) =>
        from variableName in Try(() => match.Groups[1].Value.Trim())
            .ToValidation(_ => new VariableReferenceIssue(match.Value, false))
        from validVariableName in VariableName.NewOption(variableName)
            .ToValidation(new VariableReferenceIssue(match.Value, false))
        from matchedVariable in variables.Find(validVariableName)
            .ToValidation(new VariableReferenceIssue(variableName, true))
        let value = matchedVariable.Type switch
        {
            VariableType.Boolean => Optional(matchedVariable.Value)
                .Filter(notEmpty)
                .IfNone("false"),
            VariableType.Number => Optional(matchedVariable.Value)
                .Filter(notEmpty)
                .IfNone("0"),
            _ => matchedVariable.Value
        }
        select (value, matchedVariable.Secret);

    [GeneratedRegex(@"{{\s*?(\S*?)\s*?}}")]
    private static partial Regex VariableReferenceRegex();

    private static Validation<ValidationIssue, Unit> ValidateCatletVariableInfo(
        VariableInfo variableInfo,
        string path) =>
        Optional(variableInfo.Value).Filter(notEmpty).Match(
            Some: v => VariableConfigValidations.ValidateVariableValue(v, variableInfo.Type)
                .Map(_ => unit)
                .MapFail(e => new ValidationIssue(
                    JoinPath(path, nameof(VariableConfig.Value)),
                    $"The value for the catlet variable '{variableInfo.Name}' is invalid. {e.Message}")),
            None: () => unit)
        | guard(notEmpty(variableInfo.Value) || !variableInfo.Required,
                new ValidationIssue(
                    JoinPath(path, nameof(VariableConfig.Value)),
                    $"The value for the catlet variable '{variableInfo.Name}' is required but missing."))
            .ToValidation();

    private static Validation<ValidationIssue, Unit> ValidateFodderVariableInfo(
        FodderConfig fodderConfig,
        VariableInfo variableInfo,
        string path) =>
        Optional(variableInfo.Value).Filter(notEmpty).Match(
            Some: v => VariableConfigValidations.ValidateVariableValue(v, variableInfo.Type)
                .Map(_ => unit)
                .MapFail(e => new ValidationIssue(
                    JoinPath(path, nameof(VariableConfig.Value)),
                    $"The value for the variable '{variableInfo.Name}' of the food '{fodderConfig.Name}'"
                    + Optional(fodderConfig.Source).Filter(notEmpty).Map(s => $" from '{s}'").IfNone("")
                    + $" is invalid. {e.Message}")),
            None: () => unit)
        | guard(notEmpty(variableInfo.Value) || !variableInfo.Required,
                new ValidationIssue(
                    JoinPath(path, nameof(VariableConfig.Value)),
                    $"The value for the variable '{variableInfo.Name}' of the food '{fodderConfig.Name}'"
                    + Optional(fodderConfig.Source).Filter(notEmpty).Map(s => $" from '{s}'").IfNone("")
                    + " is required but missing. The variable should be bound to a catlet variable or a constant value."))
            .ToValidation();

    private static string CreateFodderPath(string path, FodderConfig config) =>
        $"{path}["
        + Optional(config.Source).Filter(notEmpty).Map(s => $"Source={s};").IfNone("")
        + $"Name={config.Name}]";

    private static string CreateVariablePath(string path, VariableConfig config) =>
        $"{path}[Name={config.Name}]";

    private static string JoinPath(string path, string propertyName) =>
        notEmpty(path) ? $"{path}.{propertyName}" : propertyName;

    private static ValidationIssue ToValidationIssue(
        VariableReferenceIssue issue,
        FodderConfig fodderConfig,
        string path) =>
        issue.IsValid switch
        {
            true => new ValidationIssue(
                path,
                $"The variable '{issue.Reference}' referenced by the food '{fodderConfig.Name}'"
                + Optional(fodderConfig.Source).Filter(notEmpty).Map(s => $" from '{s}'").IfNone("")
                + " does not exist."),
            false => new ValidationIssue(
                path,
                $"The variable reference '{issue.Reference}' in the food '{fodderConfig.Name}'"
                + Optional(fodderConfig.Source).Filter(notEmpty).Map(s => $" from '{s}'").IfNone("")
                + " is invalid.")
        };

    private sealed record VariableInfo
    {
        public required string Name { get; init; }

        public required VariableType Type { get; init; }

        public required string Value { get; init; }

        public required bool Secret { get; init; }

        public required bool Required { get; init; }

        public static VariableInfo FromConfig(VariableConfig config) =>
            new()
            {
                Name = config.Name ?? "",
                Type = config.Type ?? VariableType.String,
                Value = config.Value ?? "",
                Secret = config.Secret ?? false,
                Required = config.Required ?? false,
            };
    }

    private sealed record VariableReferenceIssue(
        string Reference,
        bool IsValid);
}
