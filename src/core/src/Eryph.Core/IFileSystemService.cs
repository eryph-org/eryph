using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Eryph.Core
{
    public interface IFileSystemService
    {
        bool FileExists(string filePath);
        bool DirectoryExists(string path);

        Task<string> ReadAllTextAsync(string path);

        Task WriteAllTextAsync(string path, string text);

        void FileDelete(string filePath);

        string[] GetFiles(string path, string pattern, SearchOption searchOption);

        void MoveFile(string path, string newPath);
        long GetFileSize(string filePath);

        void EnsureDirectoryExists(string path);
        void DeleteOldestFiles(string path, string pattern, int filesToKeep);
        Stream OpenRead(string path);
        Stream OpenWrite(string path);

        void DeleteFile(string path);

        void DeleteDirectory(string directoryPath);

    }
}