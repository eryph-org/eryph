using System.Collections.Generic;
using System.IO;

namespace Eryph.Core
{
    public interface IFileSystemService
    {
        bool FileExists(string filePath);
        bool DirectoryExists(string path);
        byte[] ReadAllBytes(string filePath);

        void WriteAllBytes(string filePath, byte[] data);
        string ReadText(string filePath);
        void WriteText(string filePath, string data);

        void FileDelete(string filePath);
        IEnumerable<string> GetFiles(string path, string pattern);

        void MoveFile(string path, string newPath);
        long GetFileSize(string filePath);

        void EnsureDirectoryExists(string path);
        void DeleteOldestFiles(string path, string pattern, int filesToKeep);
        Stream OpenRead(string path);
        Stream OpenWrite(string path);

        void DirectoryDelete(string directoryPath);

    }
}