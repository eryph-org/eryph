using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dbosoft.CloudInit.ConfigDrive;
using Eryph.ConfigModel.Catlets;
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
                            Context.Metadata.SecureDataHidden,
                            string.IsNullOrWhiteSpace(Context.Config.Hostname) ? Context.Config.Name : Context.Config.Hostname,
                            networkData,
                            Context.Config.Fodder)
                        from newVmInfo in InsertConfigDriveDisk(configDriveIsoPath, vmInfo)
                        select newVmInfo);

                },
                () => Context.ReportProgress("Missing storage identifier - cannot generate cloud-init config drive.")
                    .ToUnit().MapAsync(_ =>
                        Prelude.RightAsync<Error, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither())
            );
        }

        private async Task<Either<Error, NetworkData>> GenerateNetworkData(TypedPsObject<VirtualMachineInfo> vmInfo)
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
                        name = adapter.Value.Name,
                        mac_address = macFormatted,
                        subnets = (Context.Config.Networks?.Filter(x=>x.AdapterName == adapter.Value.Name) 
                                   ?? new []{new CatletNetworkConfig()}.AsEnumerable())
                            .Map(nw => new
                            {
                                type = "dhcp"
                            })
                        
                    };
                    config.Add(physicalNetworkSettings);
                });

            }

            return new NetworkData(config);
        }

        private EitherAsync<Error, Unit> GenerateConfigDriveDisk(
            string configDriveIsoPath,
            bool withoutSensitive,
            string hostname,
            NetworkData networkData,
            [CanBeNull] FodderConfig[] config) =>
            Prelude.TryAsync(async () =>
            {
                var configDrive = new ConfigDriveBuilder()
                    .NoCloud(new NoCloudConfigDriveMetaData(hostname, Context.Metadata.MachineId.ToString()))
                    .Build();

                if (config != null)
                {
                    foreach (var cloudInitConfig in config)
                    {
                        if (withoutSensitive && cloudInitConfig.Secret.GetValueOrDefault())
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
                            (cloudInitConfig.Content ?? "").TrimEnd('\0'),
                            cloudInitConfig.Filename!,
                            Encoding.UTF8);
                        configDrive.AddUserData(userData);
                    }
                }

                configDrive.SetNetworkData(networkData);

                var isoWriter = new ConfigDriveImageWriter(configDriveIsoPath);
                await isoWriter.WriteConfigDrive(configDrive);

                return Unit.Default;
            }).ToEither();

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
            EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> Eject() =>
                from _ in ConvergeHelpers.FindAndApply(
                    vmInfo,
                    l => l.DVDDrives,
                    device =>
                    {
                        var drive = device.Cast<DvdDriveInfo>();
                        return drive.Value.ControllerLocation == 63 && drive.Value.ControllerNumber == 0;
                    },
                    drive => Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", drive.PsObject)
                        .AddParameter("Path", null)))
                from reloadedVmInfo in vmInfo.Reload(Context.Engine)
                select reloadedVmInfo;

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

        private EitherAsync<Error, TypedPsObject<VirtualMachineInfo>> InsertConfigDriveDisk(
            string configDriveIsoPath,
            TypedPsObject<VirtualMachineInfo> vmInfo) =>
            from dvdDrive in ConvergeHelpers.GetOrCreateInfoAsync(
                vmInfo,
                l => l.DVDDrives,
                device => device.Cast<DvdDriveInfo>()
                    .Map(drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0),
                () => Context.Engine.GetObjectAsync<VirtualMachineDeviceInfo>(
                    PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                        .AddParameter("VM", vmInfo.PsObject)
                        .AddParameter("ControllerNumber", 0)
                        .AddParameter("ControllerLocation", 63)
                        .AddParameter("PassThru")))
            from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()
                .AddCommand("Set-VMDvdDrive")
                .AddParameter("VMDvdDrive", dvdDrive.PsObject)
                .AddParameter("Path", configDriveIsoPath))
            from vmInfoRecreated in vmInfo.Reload(Context.Engine)
            select vmInfoRecreated;
    }
}