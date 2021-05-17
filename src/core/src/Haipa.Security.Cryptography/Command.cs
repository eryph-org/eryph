using System;
using System.Diagnostics;
using System.Text;

namespace Haipa.Security.Cryptography
{
    public static class Command
    {
        public static string ExecuteCommand(string command)
        {
            var stringBuilder = new StringBuilder();
            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    WindowStyle = ProcessWindowStyle.Normal,
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    Arguments = "/c " + command
                }
            })
            {
                process.Start();
                while (!process.StandardOutput.EndOfStream) stringBuilder.AppendLine(process.StandardOutput.ReadLine());
                process.Close();
            }

            return stringBuilder.ToString();
        }

        public static void RegisterSSLToUrl(CertificateOptions options)
        {
            var sslCert = ExecuteCommand("netsh http show sslcert 0.0.0.0:62189");
            if (sslCert.IndexOf(options.AppID, StringComparison.OrdinalIgnoreCase) >= 0)
                ExecuteCommand("netsh http delete sslcert ipport=0.0.0.0:62189");
            ExecuteCommand(string.Concat("netsh http add urlacl url=", options.URL));
            ExecuteCommand(
                $"netsh http add sslcert ipport=0.0.0.0:62189 certhash={options.Thumbprint} appid={{{options.AppID}}}");
        }
    }
}