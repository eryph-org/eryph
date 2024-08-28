using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LanguageExt;

using static LanguageExt.Prelude;

namespace Eryph.VmManagement;

public static class PathUtils
{
    public static Option<string> GetContainedPath(string relativeTo, string path)
    {
        if (!Path.IsPathFullyQualified(relativeTo))
            return Option<string>.None;
        
        if (!Path.IsPathFullyQualified(path))
            return None;

        var relativePath = Path.GetRelativePath(relativeTo, path);

        if(relativePath.StartsWith("..") || relativePath == "." || relativePath == path)
            return None;

        return relativePath;
    }
}
