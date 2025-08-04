using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Modules.HostAgent;

internal static class FileSystemExtensions
{
    /// <summary>
    /// Checks if the folder tree is empty which means that neither
    /// the directory nor any of its subdirectories contain any files.
    /// </summary>
    public static bool IsFolderTreeEmpty(this IDirectory directory, string path) =>
        !directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories).Any();
}