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
        new(issue.Member.AddJsonPathPrefix(prefix), issue.Message);

    public static string AddJsonPathPrefix(
        this string path,
        Option<string> prefix) =>
        prefix.Match(
            p => RootRegex().Replace(path.ToJsonPath(None), p.ToJsonPath(None)),
            () => path);

    extension<T>(Validation<ValidationIssue, T> validation)
    {
        /// <summary>
        /// Converts the given <paramref name="validation"/> to an <see cref="Either{Error, T}"/>.
        /// As part of the conversion, the property names in the <see cref="ValidationIssue"/>s
        /// are converted to proper JSON paths.
        /// </summary>
        public Either<Error, T> ToEitherWithJsonPath(string message,
            Option<JsonNamingPolicy> namingPolicy) =>
            validation.MapFail(i => i.ToJsonPath(namingPolicy))
                .MapFail(i => i.ToError())
                .ToEither()
                .MapLeft(errors => Error.New(message, Error.Many(errors)));

        public Validation<ValidationIssue, T> ToJsonPath(Option<JsonNamingPolicy> namingPolicy) =>
            validation.MapFail(vi => vi.ToJsonPath(namingPolicy));
    }

    public static ValidationIssue ToJsonPath(
        this ValidationIssue issue,
        Option<JsonNamingPolicy> namingPolicy) =>
        new(issue.Member.ToJsonPath(namingPolicy), issue.Message);

    public static string ToJsonPath(
        this string path,
        Option<JsonNamingPolicy> namingPolicy) =>
        namingPolicy.Match(
                p => NameRegex().Replace(path, m => p.ConvertName(m.Value)),
                () => path)
            .Apply(p => p.StartsWith("$.", StringComparison.Ordinal) ? p : $"$.{p}");
}
