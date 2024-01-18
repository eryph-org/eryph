using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Eryph.Runtime.Uninstaller
{
    internal class Uninstaller
    {
        private readonly bool _removeConfig;
        private readonly bool _removeVirtualMachines;
        private readonly string? _uninstallReason;
        private readonly string? _feedback;
        private Func<string, Task> _reportProgress;

        public Uninstaller(
            bool removeConfig,
            bool removeVirtualMachines,
            string? uninstallReason,
            string? feedback,
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
            var exePath = await DetectInstall();
            if (exePath is null)
                return;

            await RunUninstall(exePath);
            await RemoveFolder();
        }

        private async Task<string?> DetectInstall()
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

            return exePath;
        }

        private async Task RunUninstall(string path)
        {
            string arguments = "uninstall";
            if(_removeConfig)
                arguments += " --delete-app-data";
            if(_removeVirtualMachines)
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
            if (!Directory.Exists(folderPath))
            {
                await _reportProgress("Installation folder was not." + Environment.NewLine);
            }

            Directory.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "eryph", "zero"), true);

            await _reportProgress("Installation folder removed." + Environment.NewLine);
        }
    }
}
