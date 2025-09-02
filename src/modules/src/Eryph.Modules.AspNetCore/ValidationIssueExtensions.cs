using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.Functional.Validations;
using Eryph.Core;
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
public static partial class ValidationIssueExtensions
{
    [GeneratedRegex(@"^\$")]
    private static partial Regex RootRegex();

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
                    .Map(p => p.ToJsonPath(ApiJsonSerializerOptions.Options.PropertyNamingPolicy));

                foreach (var error in errors)
                {
                    modelState.AddModelError(AddPrefix(error.Member, jsonPathPrefix), error.Message);
                }

                return modelState;
            });

    private static string AddPrefix(string path, Option<string> prefix) =>
        prefix.Match(
            Some: p => RootRegex().Replace(path, p),
            None: () => path);
}
