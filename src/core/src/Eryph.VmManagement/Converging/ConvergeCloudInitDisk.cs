using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.CloudInit.ConfigDrive;
using Eryph.ConfigModel.Machine;
using Eryph.Modules.VmHostAgent.Networks.Powershell;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;
using LanguageExt.Common;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeCloudInitDisk : ConvergeTaskBase
    {

        public ConvergeCloudInitDisk(ConvergeContext context) : base(context)
        {
        }

        public override Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return (from vm in EjectConfigDriveDisk(vmInfo).ToAsync()
                from result in UpdateConfigDriveDisk(vm).ToAsync()
                select result).ToEither();

        }

        private string ReplaceVariables(string userData)
        {
            var sb = new StringBuilder(userData);
            sb.Replace("{{machineId}}", Context.Metadata.MachineId.ToString());
            sb.Replace("{{vmId}}", Context.Metadata.VMId.ToString());

            return sb.ToString();
        }

        public async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> UpdateConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            return await Context.StorageSettings.StorageIdentifier.MatchAsync(
                async storageIdentifier =>
                {
                    var configDriveIsoPath = Path.Combine(Context.StorageSettings.VMPath,
                        "configdrive.iso");

                    await Context.ReportProgress("Updating cloud-init config drive.").ConfigureAwait(false);

                    return await (from d in CreateConfigDriveDirectory(Context.StorageSettings.VMPath).ToAsync()
                        from networkData in GenerateNetworkData(vmInfo).ToAsync()
                        from _ in GenerateConfigDriveDisk(configDriveIsoPath,
                            Context.Metadata.SensitiveDataHidden,
                            Context.Config.Provisioning.Hostname,
                            networkData,
                            Context.Config.Provisioning.Config).ToAsync()
                        from newVmInfo in InsertConfigDriveDisk(configDriveIsoPath, vmInfo).ToAsync()
                        select newVmInfo);

                },
                () => Context.ReportProgress("Missing storage identifier - cannot generate cloud-init config drive.")
                    .ToUnit().MapAsync(_ =>
                        Prelude.RightAsync<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither())
            );
        }

        private static async Task<Either<Error, NetworkData>> GenerateNetworkData(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var config = new List<object>();
            const string regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            const string replace = "$1:$2:$3:$4:$5:$6";

            foreach (var adapterDevice in vmInfo.GetList(x=>x.NetworkAdapters))
            {
                (await adapterDevice.CastSafeAsync<VMNetworkAdapter>()).IfRight(adapter =>
                {
                    var macFormatted = Regex.Replace(adapter.Value.MacAddress, regex, replace).ToLowerInvariant();

                    var physicalNetworkSettings = new
                    {
                        type = "physical",
                        id = adapter.Value.Name,
                        name = adapter.Value.Name,
                        mac_address = macFormatted
                    };
                    config.Add(physicalNetworkSettings);
                });

            }

            return new NetworkData(config);
        }

        private Task<Either<Error, Unit>> GenerateConfigDriveDisk(string configDriveIsoPath,
            bool withoutSensitive,
            string hostname,
            NetworkData networkData,
            [CanBeNull] CloudInitConfig[] config)
        {

            return Prelude.TryAsync(async () =>
                {

                    var configDrive = new ConfigDriveBuilder()
                        .NoCloud(new NoCloudConfigDriveMetaData(hostname, Context.Metadata.MachineId.ToString()))
                        .Build();

                    if (config != null)
                    {
                        foreach (var cloudInitConfig in config)
                        {
                            if (withoutSensitive && cloudInitConfig.Sensitive)
                                continue;

                            var contentType = cloudInitConfig.Type switch
                            {
                                "include-url" => UserDataContentType.IncludeUrl,
                                "include-once-url" => UserDataContentType.IncludeUrlOnce,
                                "cloud-config-archive" => UserDataContentType.CloudConfigArchive,
                                "upstart-job" => UserDataContentType.UpstartJob,
                                "cloud-config" => UserDataContentType.CloudConfig,
                                "part-handler" => UserDataContentType.PartHandler,
                                "shellscript" => UserDataContentType.ShellScript,
                                "cloud-boothook" => UserDataContentType.BootHook,
                                _ => UserDataContentType.CloudConfig
                            };

                            var userData = new UserData(contentType, 
                                ReplaceVariables(cloudInitConfig.Content).TrimEnd('\0'),
                                cloudInitConfig.FileName,
                                Encoding.UTF8);
                            configDrive.AddUserData(userData);
                        }
                    }

                    configDrive.SetNetworkData(networkData);

                    var isoWriter = new ConfigDriveImageWriter(configDriveIsoPath);
                    await isoWriter.WriteConfigDrive(configDrive);

                    return Unit.Default;
                }).ToEither(l => new PowershellFailure { Message = l.Message })
                .ToEither().ToError();


        }


        private static Either<Error, Unit> CreateConfigDriveDirectory(string configDrivePath)
        {
            if (Directory.Exists(configDrivePath)) return Unit.Default;

            var tryResult = Prelude.Try(Directory.CreateDirectory(configDrivePath)).Try();

            if (tryResult.IsFaulted)
                return Error.New($"Failed to create directory {configDrivePath}");

            return Unit.Default;
        }

        private async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> EjectConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            async Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> Eject()
            {
                var res = await ConvergeHelpers.FindAndApply(vmInfo, l => l.DVDDrives,
                        device =>
                        {
                            var drive = device.Cast<DvdDriveInfo>();
                            return drive.Value.ControllerLocation == 63 && drive.Value.ControllerNumber == 0;
                        },
                        drive => Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Set-VMDvdDrive")
                            .AddParameter("VMDvdDrive", drive.PsObject)
                            .AddParameter("Path", null)))
                    .Map(list => list.ToArr().Lefts().HeadOrNone()).MatchAsync(
                        None: () => vmInfo.RecreateOrReload(Context.Engine).ToEither(),
                        Some: l => Prelude.LeftAsync<Error, TypedPsObject<VirtualMachineInfo>>(l.Message)
                            .ToEither());

                return res;
            }

            var res = await Eject();

            if (res.IsLeft)
                return res;

            // the eject is processed async by hyper-v - to make sure that we have really ejected the disk, wait until path is null
            var waitStarted = DateTime.Now;

            //timeout after 1 minute
            while ((DateTime.Now - waitStarted).TotalSeconds<60)
            {
                var dvd = vmInfo.GetList(x => x.DVDDrives, d =>
                {
                    var drive = d.Cast<DvdDriveInfo>();
                    return drive.Value.ControllerLocation == 63 && drive.Value.ControllerNumber == 0;
                }).FirstOrDefault()?.Cast<DvdDriveInfo>()?.Value;

                if (dvd?.Path == null) return res;

                await Task.Delay(500);

            }

            return  Error.New("Timeout while waiting for cloud-init disk to be ejected.");
        }

        private Task<Either<Error, TypedPsObject<VirtualMachineInfo>>> InsertConfigDriveDisk(
            string configDriveIsoPath,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            return
                (from dvdDrive in ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                    l => l.DVDDrives,
                    device => device.Cast<DvdDriveInfo>()
                        .Map(drive =>drive.ControllerLocation == 63 && drive.ControllerNumber == 0),
                    () => Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(
                        PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("ControllerNumber", 0)
                            .AddParameter("ControllerLocation", 63)
                            .AddParameter("PassThru"))
                ).ToAsync()
                from _ in Context.Engine.Run(PsCommandBuilder.Create()
                    .AddCommand("Set-VMDvdDrive")
                    .AddParameter("VMDvdDrive", dvdDrive.PsObject)
                    .AddParameter("Path", configDriveIsoPath)).ToAsync().ToError()
                from vmInfoRecreated in vmInfo.RecreateOrReload(Context.Engine)
                select vmInfoRecreated).ToEither();
        }
    }
}