using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.ConfigModel;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Variables;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class CatletConfigVariableSubstitutions
{
    public static Validation<ValidationIssue, CatletConfig> SubstituteVariables(
       CatletConfig config) =>
       from valuesMap in config.Variables
           .ToSeq()
           .Map((index, vc) =>
               from info in PrepareVariableInfo(vc)
                   .MapFail(e => new ValidationIssue(
                       $"{nameof(CatletConfig.Variables)}[{vc.Name}]",
                       e.Message))
               select (info.Name, info))
           .Sequence()
           .Map(values => values.ToHashMap())
       from substitutedFodder in config.Fodder.ToSeq()
           .Map(fodderConfig => SubstituteVariables(fodderConfig, valuesMap))
           .Sequence()
       select config.CloneWith(c =>
       {
           c.Fodder = substitutedFodder.ToArray();
       });

    private static Validation<ValidationIssue, FodderConfig> SubstituteVariables(
        FodderConfig config,
        HashMap<VariableName, VariableInfo> catletVariables) =>
        from fodderVariables in config.Variables.ToSeq()
            .Map(vc => from info in PrepareVariableInfo(vc)
                            .MapFail(e => new ValidationIssue($"{nameof(CatletConfig.Fodder)}[{config.Name}]", e.Message))
                       from substitutedValue in SubstituteProperty(vc, c => c.Value, v => SubstituteVariables(v, catletVariables))
                       let substitutedInfo = info with
                       {
                           Value = substitutedValue.IfNone("")
                       }
                       select (substitutedInfo.Name, substitutedInfo))
            .Sequence()
            .Map(s => s.ToHashMap().Append(catletVariables))
        from content in SubstituteProperty(config, c => c.Content, v => SubstituteVariables(v, fodderVariables))
        select config.CloneWith(c =>
        {
            c.Content = content.IfNoneUnsafe((string?)null);
        });

    private static Validation<Error, string> SubstituteVariables(
        string input,
        HashMap<VariableName, VariableInfo> values)
    {
        var errors = new List<Error>();
        var result = Regex.Replace(input, "{{(.*?)}}", match =>
            SubstituteVariable(match, values).IfFail(e =>
            {
                errors.AddRange(e);
                return match.Value;
            }));

        return errors.Count > 0
            ? Fail<Error, string>(errors.ToSeq())
            : Success<Error, string>(result);
    }

    private static Validation<Error, string> SubstituteVariable(
        Match match,
        HashMap<VariableName, VariableInfo> values) =>
        from foundName in Try(() => match.Groups[1].Value.Trim())
            .ToValidation(_ => Error.New("No variable name found."))
        from variableName in VariableName.NewValidation(foundName)
        from matchedValue in values.Find(variableName)
            .ToValidation(Error.New($"No value found for variable '{variableName}'."))
        select matchedValue.Value;


    private static Validation<ValidationIssue, Option<string>> SubstituteProperty<T>(
        T toValidate,
        Expression<Func<T, string>> getProperty,
        Func<string, Validation<Error, string>> validate,
        string path = "") =>
        SubstituteProperty(
            Optional(getProperty.Compile().Invoke(toValidate)).Filter(notEmpty),
            v => validate(v)
                .MapFail(e => new ValidationIssue(JoinPath(path, getProperty), e.Message)));

    private static Validation<ValidationIssue, Option<TProperty>> SubstituteProperty<TProperty>(
        Option<TProperty> value,
        Func<TProperty, Validation<ValidationIssue, TProperty>> validate) =>
        value.Match(
            Some: v => validate(v).Map(Some),
            None: () => Success<ValidationIssue, Option<TProperty>>(None));


    private static string JoinPath<T, TProperty>(string path, Expression<Func<T, TProperty?>> getProperty) =>
        notEmpty(path) ? $"{path}.{GetPropertyName(getProperty)}" : GetPropertyName(getProperty);

    private static string GetPropertyName<T, TProperty>(Expression<Func<T, TProperty?>> getProperty) =>
        getProperty.Body switch
        {
            MemberExpression memberExpression => memberExpression.Member.Name,
            _ => throw new ArgumentException("The expression must access and return a class member.",
                nameof(getProperty))
        };

    private static Validation<Error, VariableInfo> PrepareVariableInfo(
        VariableConfig vc) =>
        from name in VariableName.NewValidation(vc.Name)
        from _ in VariableConfigValidations.ValidateVariableValue(vc.Value, vc.Type)
        // TODO add variable value validations
        // from _ in 
        select new VariableInfo
        {
            Name = name,
            Type = vc.Type ?? VariableType.String,
            Value = vc.Value,
            Secret = vc.Secret ?? false,
            Required = vc.Required ?? false,
        };

    private record VariableInfo
    {
        public required VariableName Name { get; init; }

        public required VariableType Type { get; init; }

        public required string Value { get; init; }

        public required bool Secret { get; init; }

        public required bool Required { get; init; }
    }
}