using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Modules.AspNetCore.ApiProvider;
using LanguageExt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using static LanguageExt.Prelude;

namespace Eryph.Modules.AspNetCore;

/// <summary>
/// Provides extensions for converting <see cref="ValidationIssue"/>s
/// for use in REST API responses.
/// </summary>
/// <remarks>
/// This class converts paths, which consist of C# property names, into
/// proper JSON paths. This implementation can only handle basic JSON
/// paths which are used to describe the location of a validation issue.
/// </remarks>
public static partial class ValidationIssueExtensions
{
    [GeneratedRegex(@"^\$")]
    private static partial Regex RootRegex();

    [GeneratedRegex(@"\w[\d\w]*")]
    private static partial Regex NameRegex();

    /// <summary>
    /// <para>
    /// Converts the given <see cref="Validation"/> to a
    /// <see cref="ModelStateDictionary"/> which contains the
    /// <see cref="ValidationIssue"/>s. The result can be used with
    /// <see cref="ControllerBase.ValidationProblem(ModelStateDictionary)"/>
    /// to return an error response with the validation issues.
    /// </para>
    /// <para>
    /// The <paramref name="pathPrefix"/> parameter can be used to
    /// prefix the member path of the validation issues with a path
    /// in the API request. The <paramref name="pathPrefix"/> will
    /// be converted to a JSON path first.
    /// </para>
    /// </summary>
    public static ModelStateDictionary ToModelStateDictionary<T>(
        this Validation<ValidationIssue, T> validation,
        string? pathPrefix = null) =>
        validation.Match(
            Succ: _ => new ModelStateDictionary(),
            Fail: errors =>
            {
                var modelState = new ModelStateDictionary();
                var jsonPathPrefix = Optional(pathPrefix)
                    .Filter(notEmpty)
                    .Map(p => ToJsonPath(p, ApiJsonSerializerOptions.Options.PropertyNamingPolicy));

                foreach (var error in errors)
                {
                    modelState.AddModelError(AddPrefix(error.Member, jsonPathPrefix), error.Message);
                }

                return modelState;
            });

    public static Validation<ValidationIssue, T> ToJsonPath<T>(
        this Validation<ValidationIssue, T> validation,
        Option<JsonNamingPolicy> namingPolicy) =>
        validation.MapFail(vi => vi.ToJsonPath(namingPolicy));

    public static ValidationIssue ToJsonPath(
        this ValidationIssue issue,
        Option<JsonNamingPolicy> namingPolicy) =>
        new(ToJsonPath(issue.Member, namingPolicy), issue.Message);

    private static string ToJsonPath(
        string path,
        Option<JsonNamingPolicy> namingPolicy) =>
        namingPolicy.Match(
                Some: p => NameRegex().Replace(path, m => p.ConvertName(m.Value)),
                None: () => path)
            .Apply(p => p.StartsWith("$.", StringComparison.Ordinal) ? p : $"$.{p}");

    private static string AddPrefix(string path, Option<string> prefix) =>
        prefix.Match(
            Some: p => RootRegex().Replace(path, p),
            None: () => path);
}
