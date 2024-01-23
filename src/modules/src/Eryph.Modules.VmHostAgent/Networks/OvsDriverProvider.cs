using Eryph.Modules.VmHostAgent.Networks.Powershell;
using LanguageExt.Effects.Traits;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Eryph.Modules.VmHostAgent.Sys.ProcessRunners;
using Eryph.VmManagement;
using Eryph.VmManagement.Data.Core;
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Sys;
using LanguageExt.Sys.IO;
using LanguageExt.Sys.Traits;
using static LanguageExt.Prelude;

namespace Eryph.Modules.VmHostAgent.Networks
{
    public class OvsDriverProvider<RT> where RT : struct,
        HasCancel<RT>,
        HasLogger<RT>,
        HasPowershell<RT>,
        HasDirectory<RT>,
        HasFile<RT>,
        HasProcessRunner<RT>,
        HasEnvironment<RT>
    {
        private const string SwitchExtensionGuid = "63E968D9-754E-4704-A5CE-6E3BF7DDF59B";
        private const string DriverModuleName = "DBO_OVSE";

        public static Aff<RT, Unit> ensureDriver(
            string ovsRunDir,
            bool canInstall,
            bool canUpgrade)
        {
            return from extensionInfo in getExtensionInfo()
                   let isAlreadyInstalled = extensionInfo.IsSome
                   from _ in match(extensionInfo,
                       Some: ei => logInformation("Hyper-V switch extension {ExtensionVersion} is installed", ei.Version),
                       None: () => logInformation("Hyper-V switch extension is not installed"))
                   let infPath = Path.Combine(ovsRunDir, "driver", "dbo_ovse.inf")
                   from infVersion in getDriverVersionFromInfFile(infPath)
                   from __ in match(extensionInfo,
                       Some: ei => from extensionVersion in parseVersion(ei.Version).ToAff(Error.New("Could not parse version of Hyper-V extension"))
                                   from _ in extensionVersion != infVersion && canUpgrade 
                                       ? from _ in removeAllDriverPackages()
                                         from __ in installDriver(infPath)
                                         select unit
                                       : from _ in extensionVersion != infVersion
                                            ? logWarning("Hyper-V switch extension version {ExtensionVersion} does not match driver version {DriverVersion}",
                                                ei.Version, infVersion)
                                            : SuccessAff<RT, Unit>(unit)
                                         select unit
                                   select unit,
                       None: () => canInstall ? installDriver(infPath) : FailAff<RT, Unit>(Error.New("Hyper-V switch extension is missing")))
                   //from isDriverLoaded in isDriverLoaded()
                   //from ___ in guard(isDriverLoaded, Error.New("Hyper-V switch extension driver was not loaded. Consider reinstalling the driver."))
                   select unit;
        }

        public static Aff<RT, Unit> installDriver(string infPath)
        {
            return from _ in logInformation("Going to install OVS Hyper-V switch extension")
                   from systemFolderPath in Environment<RT>.getFolderPath(Environment.SpecialFolder.System)
                   from newLine in Environment<RT>.newLine
                   let netCfgPath = Path.Combine(systemFolderPath, "netcfg.exe")
                   let infFileName = Path.GetFileName(infPath)
                   let infDirectoryPath = Path.GetDirectoryName(infPath)
                   from result in ProcessRunner<RT>.runProcess(
                       netCfgPath, @$"-l ""{infFileName}"" -c s -i {DriverModuleName}", infDirectoryPath)
                   // TODO better error handling
                   from __ in guard(result.ExitCode == 0, Error.New($"Failed to install driver:{newLine}{result.Output}"))
                   from ___ in logInformation("Successfully installed OVS Hyper-V switch extension...")
                   select unit;
        }

        public static Aff<RT, Unit> uninstallDriver()
        {
            return from _ in logInformation("Going to uninstall OVS Hyper-V switch extension...")
                   from systemFolderPath in Environment<RT>.getFolderPath(Environment.SpecialFolder.System)
                   from newLine in Environment<RT>.newLine
                   let netCfgPath = Path.Combine(systemFolderPath, "netcfg.exe")
                   from result in ProcessRunner<RT>.runProcess(netCfgPath, $"/u {DriverModuleName}")
                   from __ in guard(result.ExitCode == 0, Error.New($"Failed to uninstall driver:{newLine}{result.Output}"))
                   from ___ in logInformation("Successfully uninstalled OVS Hyper-V switch extension...")
                   select unit;
        }

        public static Aff<RT, Unit> removeAllDriverPackages()
        {
            return from installedDriverPackages in getInstalledDriverPackages()
                   from ___ in installedDriverPackages
                       .Map(di => removeDriverPackage(di.Driver))
                       .TraverseSerial(u => u)
                   select unit;
        }


        public static Aff<RT, Unit> removeDriverPackage(string infName)
        {
            return from _ in logInformation("Going to remove driver package {InfName}...", infName)
                   from result in ProcessRunner<RT>.runProcess("pnputil.exe", $"/delete-driver {infName}", "")
                   from newLine in Environment<RT>.newLine
                   from __ in guard(result.ExitCode == 0, Error.New(
                       $"Failed to remove driver package {infName}:{newLine}{result.Output}"))
                   from ___ in logInformation("Successfully removed driver package {InfName}...", infName)
                   select unit;
        }

        public static Aff<RT, Seq<DismDriverInfo>> getInstalledDriverPackages()
        {
            return from psEngine in default(RT).Powershell
                   let command = PsCommandBuilder.Create()
                       .AddCommand("Get-WindowsDriver")
                       .AddParameter("Online")
                   from result in psEngine.GetObjectsAsync<DismDriverInfo>(command).ToAff()
                   select result.Select(r => Optional(r.Value))
                       .Somes()
                       .Filter(di => di.OriginalFileName?.Contains(
                            DriverModuleName, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public static Aff<RT, bool> isDriverLoaded()
        {
            return from result in ProcessRunner<RT>.runProcess("driverquery.exe", "/FO LIST")
                   from match in Eff(() => Regex.Match(
                       result.Output,
                       $@"Module Name:\s*{Regex.Escape(DriverModuleName)}",
                       RegexOptions.Multiline | RegexOptions.IgnoreCase))
                   select match.Success;
        }

        public static Aff<RT, Option<VmSystemSwitchExtension>> getExtensionInfo()
        {
            return from psEngine in default(RT).Powershell
                let command = PsCommandBuilder.Create().AddCommand("Get-VMSystemSwitchExtension")
                from results in psEngine.GetObjectsAsync<VmSystemSwitchExtension>(command).ToAff()
                select results.Select(r => r.Value)
                    .Where(e => string.Equals(e.Id, SwitchExtensionGuid, StringComparison.OrdinalIgnoreCase))
                    .HeadOrNone();
        }

        public static Aff<RT, Version> getDriverVersionFromInfFile(string filePath)
        {
            return from fileContent in getInfFileContent(filePath)
                   from version in extractDriverVersionFromInf(fileContent)
                   select version;
        }

        internal static Aff<RT, string> getInfFileContent(string filePath)
        {
            return from bytes in File<RT>.readAllBytes(filePath)
                   // INF files can be encoded in UTF-16 LE (preferred) or Windows code pages.
                   // We detect the encoding by checking for the UTF-16 LE BOM.
                   from content in Seq<byte>(0xFF, 0xFE) == bytes.Take(2).ToSeq()
                       ? Eff<RT, string>(_ => Encoding.Unicode.GetString(bytes.Skip(2).ToArray()))
                       // .NET Core does not support Windows code pages, so we fall back to ASCII.
                       : Eff<RT, string>(_ => Encoding.ASCII.GetString(bytes))
                   select content;
        }   

        internal static Eff<Version> extractDriverVersionFromInf(string infContent)
        {
            return from match in Eff(() => Regex.Match(
                        infContent,
                        @"DriverVer\s*=.*,(\d+\.\d+\.\d+\.\d+)",
                        RegexOptions.Multiline | RegexOptions.IgnoreCase))
                   from _ in guard(match.Success, Error.New("Could not extract driver version from INF"))
                   from version in parseVersion(match.Groups[1].Value).ToEff(Error.New("Could not parse driver version"))
                   select version;
        }

        private static Option<Version> parseVersion(string input)
        {
            return Version.TryParse(input, out var version)
                ? Some(version)
                : None;
        }

        private static Eff<RT, Unit> logInformation(string message, params object[] args)
            => Logger<RT>.logInformation<OvsDriverProvider<RT>>(message, args);

        private static Eff<RT, Unit> logWarning(string message, params object[] args)
            => Logger<RT>.logWarning<OvsDriverProvider<RT>>(message, args);
    }
}
