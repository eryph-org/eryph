using Dbosoft.Functional.Validations;
using LanguageExt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
