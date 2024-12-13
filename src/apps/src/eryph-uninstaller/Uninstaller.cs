using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Eryph.Runtime.Uninstaller
{
    internal class Uninstaller
    {
        private readonly bool _removeConfig;
        private readonly bool _removeVirtualMachines;
        private readonly UninstallReason _uninstallReason;
        private readonly string _feedback;
        private readonly Func<string, Task> _reportProgress;

        private const string DataPlaneUrl = "https://dp-t.dbosoft.eu/v1/track";
        private const string WriteKey = "2bApAyC76MQAPXOjMkUlrToX7zD";

        public Uninstaller(
            bool removeConfig,
            bool removeVirtualMachines,
            UninstallReason uninstallReason,
            string feedback,
            Func<string, Task> reportProgress)
        {
            _removeConfig = removeConfig;
            _removeVirtualMachines = removeVirtualMachines;
            _uninstallReason = uninstallReason;
            _feedback = feedback;
            _reportProgress = reportProgress;
        }

        public async Task UninstallAsync()
        {
            try
            {
                var fileVersionInfo = await DetectInstall();
                if (fileVersionInfo is null)
                    return;

                await TrackAsync(fileVersionInfo.ProductVersion);
                await RunUninstall(fileVersionInfo.FileName);
                await RemoveFolder();
            }
            catch (Exception e)
            {
                await _reportProgress(e.ToString());
            }
        }

        public async Task TrackAsync(string productVersion)
        {
            try
            {
                using var client = new HttpClient();

                var json = $$"""
                 {
                     "anonymousId": "{{Guid.NewGuid()}}",
                     "event": "uninstall_eryph",
                     "properties": {
                         "product": "eryph_zero",
                         "version": "{{HttpUtility.JavaScriptStringEncode(productVersion)}}",
                         "uninstall_reason": "{{_uninstallReason:G}}",
                         "feedback": "{{HttpUtility.JavaScriptStringEncode(_feedback)}}"
                     }
                 }
                 """;

                using var request = new HttpRequestMessage();

                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(DataPlaneUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{WriteKey}:")));
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception)
            {
                // Ignore any exceptions when sending tracking request unless we are debugging
#if DEBUG
                if (Debugger.IsAttached)
                    throw;
#endif
            }
        }

        public async Task<FileVersionInfo?> DetectInstall()
        {
            await _reportProgress("Detecting eryph installation..." + Environment.NewLine);

            var exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph", "zero", "bin", "eryph-zero.exe");
            if (!File.Exists(exePath))
            {
                await _reportProgress("Eryph installation not found." + Environment.NewLine);
                return null;
            }

            var fileVersionInfo = FileVersionInfo.GetVersionInfo(exePath);
            await _reportProgress($"Eryph version {fileVersionInfo.ProductVersion} found." + Environment.NewLine);

            return fileVersionInfo;
        }

        private async Task RunUninstall(string path)
        {
            string arguments = "uninstall";
            if (_removeConfig)
                arguments += " --delete-app-data";
            if (_removeVirtualMachines)
                arguments += " --delete-catlets";
            
            var processStartInfo = new ProcessStartInfo(path)
            {
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                CreateNoWindow = true
            };

            using var process = Process.Start(processStartInfo);
            if (process is null)
            {
                await _reportProgress("Failed to start uninstallation");
                return;
            }

            do
            {
                var line = await process.StandardOutput.ReadLineAsync();
                await _reportProgress(line + Environment.NewLine);
            } while (!process.StandardOutput.EndOfStream);

            process.WaitForExit();
        }

        private async Task RemoveFolder()
        {
            await _reportProgress("Removing installation folder..." + Environment.NewLine);
            
            var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph", "zero");
            var eryphPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph");
            var eryphZeroPath = Path.Combine(eryphPath, "zero");
            
            if (!Directory.Exists(eryphZeroPath))
            {
                await _reportProgress("Installation folder was not found." + Environment.NewLine);
                return;
            }

            try
            {
                Directory.Delete(folderPath, true);

                if (!Directory.EnumerateFileSystemEntries(eryphPath).Any())
                {
                    Directory.Delete(eryphPath);
                }
            }
            catch (Exception e)
            {
                await _reportProgress(
                    $"The installation folder could not be removed. Please delete the files manually from '{folderPath}'."
                    + Environment.NewLine);
            }

            await _reportProgress("Installation folder removed." + Environment.NewLine);
        }
    }
}
