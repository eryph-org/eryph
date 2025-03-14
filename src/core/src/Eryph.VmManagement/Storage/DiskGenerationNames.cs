using System;
using System.IO;
using System.Text.RegularExpressions;
using LanguageExt;
using LanguageExt.Common;

using static Eryph.Core.RegexPrelude;
using static LanguageExt.Prelude;

namespace Eryph.VmManagement.Storage;

public static partial class DiskGenerationNames
{
    [GeneratedRegex(@"_g(\d+)$", RegexOptions.Compiled)]
    private static partial Regex GenerationSuffixRegex();

    public static Either<Error, string> AddGenerationSuffix(string diskPath, int generation) =>
        from directoryPath in Try(() => Path.GetDirectoryName(diskPath))
            .ToOption()
            .Filter(notEmpty)
            .ToInvalidPathError(diskPath)
        from nameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(diskPath))
            .ToOption()
            .Filter(notEmpty)
            .ToInvalidPathError(diskPath)
        from extension in Try(() => Path.GetExtension(diskPath))
            .ToOption()
            .Filter(notEmpty)
            .ToInvalidPathError(diskPath)
        let result = generation > 0
            ? Path.Combine(directoryPath, $"{nameWithoutExtension}_g{generation}{extension}")
            : diskPath
        select result;

    public static Either<Error, string> GetFileNameWithoutSuffix(
        string path,
        Option<string> parentPath) =>
        from nameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(path))
            .ToOption()
            .Filter(notEmpty)
            .ToInvalidPathError(path)
        from result in parentPath.Match(
            Some: p => GetFileNameWithoutSuffix(nameWithoutExtension, p),
            None: () => nameWithoutExtension)
        select result;

    private static Either<Error, string> GetFileNameWithoutSuffix(
        string fileName,
        string parentPath) =>
        from parentNameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(parentPath))
            .ToOption()
            .Filter(notEmpty)
            .ToEither(Error.New($"The parent disk path '{parentPath}' is invalid."))
        from generation in GetGenerationFromFileName(fileName)
        from parentGeneration in GetGenerationFromFileName(parentNameWithoutExtension)
        let suffix = generation
            .Filter(g => g == parentGeneration.IfNone(0) + 1)
            .Map(g => $"_g{g}")
        let index = suffix
            .Map(s => fileName.IndexOf(s, StringComparison.OrdinalIgnoreCase))
            .Filter(i => i > 0)
        select index.Match(
            Some: i => fileName[..i],
            None: () => fileName);
    
    private static Either<Error, Option<int>> GetGenerationFromFileName(string fileName) =>
        from match in regexMatch(GenerationSuffixRegex(), fileName).ToEither()
        let result = from m in match
                     from g in regexGroup(m.Groups, 1)
                     from generation in parseInt(g.Value)
                     select generation
        select result;

    private static Either<Error, string> ToInvalidPathError(
        this Option<string> value,
        string path) =>
        value.ToEither(Error.New($"The disk path '{path}' is invalid."));
}
