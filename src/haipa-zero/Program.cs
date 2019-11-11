using Haipa.Security.Cryptography;
using Haipa.Modules.Hosting;
using SimpleInjector;
using System;
using System.Diagnostics;
using System.IO;

namespace Haipa.Runtime.Zero
﻿{
    /// <summary>
    /// Defines the <see cref="Program" />
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// The Main
        /// </summary>
        /// <param name="args">The args<see cref="string[]"/></param>
        private static void Main(string[] args)
        {
 
            ConfigStore.Config.EnsureConfigPaths();

            File.WriteAllText(Path.Combine(ConfigStore.Config.GetConfigPath(), "zero_info"),
                $"{{ \"process_id\": \"{Process.GetCurrentProcess().Id}\", \"url\" : \"http://localhost:62189\" }}");

            Certificate.CreateSSL(new CertificateOptions
            {
                Issuer = Network.FQDN,
                FriendlyName = "Haipa Zero Management Certificate",
                Suffix = "CA",
                ValidStartDate = DateTime.UtcNow,
                ValidEndDate = DateTime.UtcNow.AddYears(5),
                Password = "password",
                ExportDirectory = Directory.GetCurrentDirectory(),
                URL = "https://localhost:62189/",
                AppID = "9412ee86-c21b-4eb8-bd89-f650fbf44931",
                CACertName = "HaipaCA.pfx"
            });

            var container = new Container();
            container.Bootstrap(args);
            container.RunModuleHostService("haipa-zero");
        }
    }
 }
