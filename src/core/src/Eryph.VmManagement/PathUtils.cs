using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class PathUtils
{
    public static Option<string> GetContainedPath(string relativeTo, string path) =>
        from _ in Some(unit)
        where Path.IsPathFullyQualified(relativeTo)
        where Path.IsPathFullyQualified(path)
        let relativePath = Path.GetRelativePath(relativeTo, path)
        where !relativePath.StartsWith("..") && relativePath != "." && relativePath != path
        select relativePath;
}
