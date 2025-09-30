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
        private readonly FeedbackData _feedbackData;
        private readonly Func<string, Task> _reportProgress;

        private const string DataPlaneUrl = "https://dp-t.dbosoft.eu/v1/track";
        private const string WriteKey = "2bApAyC76MQAPXOjMkUlrToX7zD";

        public Uninstaller(
            bool removeConfig,
            bool removeVirtualMachines,
            FeedbackData feedbackData,
            Func<string, Task> reportProgress)
        {
            _removeConfig = removeConfig;
            _removeVirtualMachines = removeVirtualMachines;
            _feedbackData = feedbackData;
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
                         "product_version": "{{HttpUtility.JavaScriptStringEncode(productVersion)}}",
                         "uninstall_reason": "{{_feedbackData.UninstallReason:G}}",
                         "technical_issue_type": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.TechnicalIssueType ?? "")}}",
                         "technical_issue_details": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.TechnicalIssueDetails ?? "")}}",
                         "technical_issue_email": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.TechnicalIssueEmail ?? "")}}",
                         "additional_feedback_text": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.AdditionalFeedbackText ?? "")}}",
                         "feedback_email": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.FeedbackEmail ?? "")}}",
                         "remove_config": "{{_feedbackData.RemoveConfig.ToString().ToLower()}}",
                         "remove_virtual_machines": "{{_feedbackData.RemoveVirtualMachines.ToString().ToLower()}}",
                         "feedback_source": "{{HttpUtility.JavaScriptStringEncode(_feedbackData.FeedbackSource)}}"
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

        public static FileVersionInfo? GetEryphVersion()
        {
            var exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph", "zero", "bin", "eryph-zero.exe");

            if (!File.Exists(exePath))
                return null;

            try
            {
                var fileVersionInfo = FileVersionInfo.GetVersionInfo(exePath);
                return fileVersionInfo;
            }
            catch
            {
                return null;
            }
        }

        public async Task<FileVersionInfo?> DetectInstall()
        {
            await _reportProgress("Detecting eryph installation..." + Environment.NewLine);

            var version = GetEryphVersion();

            if(version!= null)
                await _reportProgress($"eryph version {version.ProductVersion} found." + Environment.NewLine);
            else
                await _reportProgress($"eryph-zero not found." + Environment.NewLine);

            return version;
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
            catch (Exception)
            {
                await _reportProgress(
                    $"The installation folder could not be removed. Please delete the files manually from '{folderPath}'."
                    + Environment.NewLine);
                return;
            }

            await _reportProgress("Installation folder removed." + Environment.NewLine);
        }
    }
}
