using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using Eryph.Core;
using LanguageExt;
using LanguageExt.Common;

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
               from _ in ValidateVariableInfo(info, 
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
                variableConfig,
                CreateVariablePath(JoinPath(path, nameof(FodderConfig.Variables)), variableConfig),
                catletVariables))
            .Sequence()
            .Map(s => s.ToHashMap().Append(catletVariables))
        from result in SubstituteVariables(config.Content ?? "", combinedVariables)
            .MapFail(e => new ValidationIssue(
                JoinPath(path, nameof(FodderConfig.Content)),
                e.Message))
        select config.CloneWith(c =>
        {
            c.Content = result.Value;
            c.Secret = result.Secret;
        });

    private static Validation<ValidationIssue, (VariableName Name, VariableInfo Info)> SubstituteVariables(
        VariableConfig config,
        string path,
        HashMap<VariableName, VariableInfo> catletVariables) =>
        from name in VariableName.NewValidation(config.Name)
            .MapFail(e => new ValidationIssue(JoinPath(path, nameof(VariableConfig.Name)), e.Message))
        from t in SubstituteVariables(config.Value ?? "", catletVariables)
            .MapFail(e => new ValidationIssue(JoinPath(path, nameof(VariableConfig.Value)), e.Message))
        let info = VariableInfo.FromConfig(config)
        let substitutedInfo = info with
        {
            Value = t.Value,
            Secret = info.Secret || t.Secret,
        }
        from _ in ValidateVariableInfo(substitutedInfo, path)
        select (name, substitutedInfo);

    private static Validation<Error, (string Value, bool Secret)> SubstituteVariables(
        string input,
        HashMap<VariableName, VariableInfo> values)
    {
        var isSecret = false;
        var errors = new List<Error>();
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

    private static Validation<Error, (string Value, bool Secret)> MatchVariable(
        Match match,
        HashMap<VariableName, VariableInfo> variables) =>

        from variableName in Try(() => match.Groups[1].Value.Trim())
            .ToValidation(_ => Error.New($"The variable reference '{match.Value}' is invalid."))
        from validVariableName in VariableName.NewOption(variableName)
            .ToValidation(Error.New($"The variable reference '{match.Value}' is invalid."))
        from matchedVariable in variables.Find(validVariableName)
            .ToValidation(Error.New($"The referenced variable '{variableName}' does not exist."))
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

    private static Validation<ValidationIssue, Unit> ValidateVariableInfo(
        VariableInfo variableInfo,
        string path) =>
        VariableConfigValidations.ValidateVariableValue(variableInfo.Value, variableInfo.Type)
            .Map(_ => unit)
            .MapFail(e => new ValidationIssue(JoinPath(path, nameof(VariableConfig.Value)), e.Message))
        | guard(notEmpty(variableInfo.Value) || !variableInfo.Required,
                new ValidationIssue(
                    JoinPath(path, nameof(VariableConfig.Value)),
                    "The value is required but missing."))
            .ToValidation();

    private static string CreateFodderPath(string path, FodderConfig config) =>
        $"{path}["
        + Optional(config.Source).Filter(notEmpty).Map(s => $"Source={s};").IfNone("")
        + $"Name={config.Name}]";

    private static string CreateVariablePath(string path, VariableConfig config) =>
        $"{path}[Name={config.Name}]";

    private static string JoinPath(string path, string propertyName) =>
        notEmpty(path) ? $"{path}.{propertyName}" : propertyName;

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
}
