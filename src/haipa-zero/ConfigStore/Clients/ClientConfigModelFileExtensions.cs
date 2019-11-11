using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Haipa.Runtime.Zero.ConfigStore.Clients
{
    internal static class ClientConfigModelFileExtensions
    {
        public static Task SaveConfigFile(this ClientConfigModel c)
        {
            var json = JsonConvert.SerializeObject(c);
            var filePath = Path.Combine(Config.GetConfigPath(), c.ClientId + ".json");
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

        public static void DeleteConfigFile(this ClientConfigModel c)
        {
            var filePath = Path.Combine(Config.GetConfigPath(), c.ClientId + ".json");
            if(File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}