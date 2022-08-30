using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eryph.Runtime.Zero.Configuration
{
    internal class ConfigIO
    {
        private readonly string _basePath;

        public ConfigIO(string basePath)
        {
            _basePath = basePath;
        }

        public Task SaveConfigFile<T>(T data, string id)
        {
            var json = JsonSerializer.Serialize(data);
            var filePath = Path.Combine(_basePath, CoerceValidFileName(id) + ".json");
            return SaveFileOperation(json, filePath);
        }

        private static async Task SaveFileOperation(string content, string path)
        {
            var tempPathExtension = Path.GetExtension(path);
            var filePathTemp = Path.ChangeExtension(path, $"{tempPathExtension}.new");

            try
            {
                await File.WriteAllTextAsync(filePathTemp, content).ConfigureAwait(false);
                File.Copy(filePathTemp, path, true);
            }
            finally
            {
                if (File.Exists(filePathTemp))
                    File.Delete(filePathTemp);
            }
        }

        /// <summary>
        ///     Strip illegal chars and reserved words from a candidate filename (should not include the directory path)
        /// </summary>
        /// <remarks>
        ///     http://stackoverflow.com/questions/309485/c-sharp-sanitize-file-name
        /// </remarks>
        public static string CoerceValidFileName(string filename)
        {
            var invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
            var invalidReStr = $@"[{invalidChars}]+";

            var reservedWords = new[]
            {
                "CON", "PRN", "AUX", "CLOCK$", "NUL", "COM0", "COM1", "COM2", "COM3", "COM4",
                "COM5", "COM6", "COM7", "COM8", "COM9", "LPT0", "LPT1", "LPT2", "LPT3", "LPT4",
                "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            };

            var sanitizedNamePart = Regex.Replace(filename, invalidReStr, "_");
            for (var index = 0; index < reservedWords.Length; index++)
            {
                var reservedWord = reservedWords[index];
                var reservedWordPattern = $"^{reservedWord}(\\.|$)";

                sanitizedNamePart = Regex.Replace(sanitizedNamePart, reservedWordPattern, $"_RW{index}_$1",
                    RegexOptions.IgnoreCase);
            }

            return sanitizedNamePart;
        }

        public void DeleteConfigFile(string id)
        {
            var filePath = Path.Combine(_basePath, CoerceValidFileName(id) + ".json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}