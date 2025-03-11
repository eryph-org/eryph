using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.VmManagement.Data;
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
            .ToEither(_ => Error.New("BUH"))
        from nameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(diskPath))
            .ToEither(_ => Error.New("BUH"))
        from extension in Try(() => Path.GetExtension(diskPath))
            .ToEither(_ => Error.New("BUH"))
        let result = Path.Combine(directoryPath, $"{nameWithoutExtension}_g{generation}.{extension}")
        select result;

    public static Either<Error, string> GetFileNameWithoutSuffix(
        string path,
        Option<string> parentPath) =>
        from nameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(path))
            .ToEither(_ => Error.New("BUH"))
        from result in parentPath.Match(
            Some: p => GetFileNameWithoutSuffix(nameWithoutExtension, p),
            None: () => nameWithoutExtension)
        select result;

    private static Either<Error, string> GetFileNameWithoutSuffix(
        string fileName,
        string parentPath) =>
        from parentNameWithoutExtension in Try(() => Path.GetFileNameWithoutExtension(parentPath))
            .ToEither(_ => Error.New("BUH"))
        let i = fileName.IndexOf("_g", StringComparison.OrdinalIgnoreCase)
        select i > 0 ? fileName[..i] : fileName;

    
    private static Either<Error, Option<int>> GetGenerationFromFileName(string fileName) =>
        from match in regexMatch(GenerationSuffixRegex(), fileName).ToEither()
        let result = from m in match
                     from g in regexGroup(m.Groups, 1)
                     from generation in parseInt(g.Value)
                     select generation
        select result;
}

