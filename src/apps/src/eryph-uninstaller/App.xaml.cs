using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Eryph.Runtime.Uninstaller
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private bool _deleteOnExit = false;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
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
            
            var startInfo = new ProcessStartInfo(tempExecutablePath)
            {
                Arguments = "-continue",
                UseShellExecute = true,
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

            var startInfo = new ProcessStartInfo("powershell.exe", "")
            {
                Arguments = @$"-C ""Wait-Process -Id {currentProcess.Id}; Remove-Item -LiteralPath '{executablePath}';Remove-Item -LiteralPath '{folderPath}';""",
                UseShellExecute = true,
                CreateNoWindow = true,
            };
            Process.Start(startInfo);
        }
    }
}
