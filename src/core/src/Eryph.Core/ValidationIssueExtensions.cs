using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dbosoft.Functional.Validations;
using LanguageExt;
using LanguageExt.Common;

using static LanguageExt.Prelude;

namespace Eryph.Core;

/// <summary>
/// Provides extensions for converting the paths in <see cref="ValidationIssue"/>s
/// to proper JSON paths.
/// </summary>
/// <remarks>
/// This class converts paths, which consist of C# property names, into
/// proper JSON paths. This implementation can only handle basic JSON
/// paths which are used to describe the location of a validation issue.
/// </remarks>
public static partial class ValidationIssueExtensions
{
    [GeneratedRegex(@"\w[\d\w]*")]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"^\$")]
    private static partial Regex RootRegex();

    public static Validation<ValidationIssue, T> AddJsonPathPrefix<T>(
        this Validation<ValidationIssue, T> validation,
        Option<string> prefix) =>
        validation.MapFail(vi => vi.AddJsonPathPrefix(prefix));

    public static ValidationIssue AddJsonPathPrefix(
        this ValidationIssue issue,
        Option<string> prefix) =>
        new(AddJsonPathPrefix(issue.Member, prefix), issue.Message);

    public static string AddJsonPathPrefix(
        this string path,
        Option<string> prefix) =>
        prefix.Match(
            Some: p => RootRegex().Replace(ToJsonPath(path, None), ToJsonPath(p, None)),
            None: () => path);

    /// <summary>
    /// Converts the given <paramref name="validation"/> to an <see cref="Either{Error, T}"/>.
    /// As part of the conversion, the property names in the <see cref="ValidationIssue"/>s
    /// are converted to proper JSON paths.
    /// </summary>
    public static Either<Error, T> ToEitherWithJsonPath<T>(
        this Validation<ValidationIssue, T> validation,
        string message,
        Option<JsonNamingPolicy> namingPolicy) =>
        validation.MapFail(i => i.ToJsonPath(namingPolicy))
            .MapFail(i => i.ToError())
            .ToEither()
            .MapLeft(errors => Error.New(message, Error.Many(errors)));

    public static Validation<ValidationIssue, T> ToJsonPath<T>(
        this Validation<ValidationIssue, T> validation,
        Option<JsonNamingPolicy> namingPolicy) =>
        validation.MapFail(vi => vi.ToJsonPath(namingPolicy));

    public static ValidationIssue ToJsonPath(
        this ValidationIssue issue,
        Option<JsonNamingPolicy> namingPolicy) =>
        new(ToJsonPath(issue.Member, namingPolicy), issue.Message);

    public static string ToJsonPath(
        this string path,
        Option<JsonNamingPolicy> namingPolicy) =>
        namingPolicy.Match(
                Some: p => NameRegex().Replace(path, m => p.ConvertName(m.Value)),
                None: () => path)
            .Apply(p => p.StartsWith("$.", StringComparison.Ordinal) ? p : $"$.{p}");
}
