using System;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _deleteOnExit;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
#if DEBUG
            if (Debugger.IsAttached)
                return;
#endif
            if (e.Args.Length > 0 && e.Args[0] == "-continue")
            {
                _deleteOnExit = true;
                return;
            }

            var currentProcess = Process.GetCurrentProcess();
            var currentExecutablePath = currentProcess!.MainModule!.FileName;
            var tempFolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempExecutablePath = Path.Combine(tempFolderPath, Path.GetFileName(currentExecutablePath));
            
            Directory.CreateDirectory(tempFolderPath);
            File.Copy(currentExecutablePath, tempExecutablePath, true);

            var appConfig = currentExecutablePath + ".config";
            var tempAppConfigPath = tempExecutablePath + ".config";
            if (File.Exists(appConfig))
                File.Copy(appConfig, tempAppConfigPath, true);

            var startInfo = new ProcessStartInfo(tempExecutablePath)
            {
                Arguments = "-continue",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = tempFolderPath,
            };
            Process.Start(startInfo);
            
            Shutdown();
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            if (!_deleteOnExit)
                return;
            
            var currentProcess = Process.GetCurrentProcess();
            var executablePath = currentProcess!.MainModule!.FileName;
            var folderPath = Path.GetDirectoryName(executablePath);
            var configPath = executablePath + ".config";

            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                Arguments = $"""-C "Wait-Process -Id {currentProcess.Id}; Remove-Item -LiteralPath '{executablePath}'; Remove-Item -LiteralPath '{configPath}';Remove-Item -LiteralPath '{folderPath}';" """,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetTempPath(),
            };
            Process.Start(startInfo);
        }
    }
}
