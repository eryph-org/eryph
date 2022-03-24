using System;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.CloudInit.ConfigDrive;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using JetBrains.Annotations;
using LanguageExt;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeCloudInitDisk : ConvergeTaskBase
    {

        public ConvergeCloudInitDisk(ConvergeContext context) : base(context)
        {
        }

        public override Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
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

        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> UpdateConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            return await Context.StorageSettings.StorageIdentifier.MatchAsync(
                async storageIdentifier =>
                {
                    var configDriveIsoPath = Path.Combine(Context.StorageSettings.VMPath, storageIdentifier,
                        "configdrive.iso");

                    await Context.ReportProgress("Updating cloud-init config drive.").ConfigureAwait(false);

                    return await (from d in CreateConfigDriveDirectory(Context.StorageSettings.VMPath).ToAsync()
                           from _ in GenerateConfigDriveDisk(configDriveIsoPath,
                               Context.Metadata.SensitiveDataHidden,
                               Context.Config.Provisioning.Hostname,
                               Context.Config.Provisioning.Config).ToAsync()
                           from newVmInfo in InsertConfigDriveDisk(configDriveIsoPath, vmInfo).ToAsync()
                           select newVmInfo);
                    
                },
                () => Context.ReportProgress("Missing storage identifier - cannot generate cloud-init config drive.")
                    .ToUnit().MapAsync(_ =>
                        Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither())
            );
        }

        private Task<Either<PowershellFailure, Unit>> GenerateConfigDriveDisk(string configDriveIsoPath,
            bool withoutSensitive,
            string hostname,
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

                            var userData = new UserData(contentType, ReplaceVariables(cloudInitConfig.Content),
                                cloudInitConfig.FileName,
                                Encoding.UTF8);
                            configDrive.AddUserData(userData);
                        }
                    }

                    var isoWriter = new ConfigDriveImageWriter(configDriveIsoPath);
                    await isoWriter.WriteConfigDrive(configDrive);

                    return Unit.Default;
                }).ToEither(l => new PowershellFailure { Message = l.Message })
                .ToEither();


        }


        private static Either<PowershellFailure, Unit> CreateConfigDriveDirectory(string configDrivePath)
        {
            if (Directory.Exists(configDrivePath)) return Unit.Default;

            var tryResult = Prelude.Try(Directory.CreateDirectory(configDrivePath)).Try();

            if (tryResult.IsFaulted)
                return new PowershellFailure {Message = $"Failed to create directory {configDrivePath}"};

            return Unit.Default;
        }

        private async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EjectConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Eject()
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
                        None: () => vmInfo.RecreateOrReload(Context.Engine),
                        Some: l => Prelude.LeftAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(l)
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

            return new PowershellFailure { Message = "Timeout while waiting for cloud-init disk to be ejected." };
        }


        //private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> RemoveConfigDriveDisk(
        //    TypedPsObject<VirtualMachineInfo> vmInfo)
        //{
        //    return ConvergeHelpers.FindAndApply(vmInfo, l => l.DVDDrives,
        //            device =>
        //                device.Cast<DvdDriveInfo>().Map(drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0),
        //            drive =>
        //            {
        //                return Context.Engine.RunAsync(PsCommandBuilder.Create()
        //                    .AddCommand("Remove-VMDvdDrive")
        //                    .AddParameter("VMDvdDrive", drive.PsObject));
        //            })
        //        .Map(list => list.Lefts().HeadOrNone()).MatchAsync(
        //            None: () => vmInfo.RecreateOrReload(Context.Engine),
        //            Some: l => Prelude.LeftAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(l).ToEither());
        //}

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> InsertConfigDriveDisk(
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
                    .AddParameter("Path", configDriveIsoPath)).ToAsync()
                from vmInfoRecreated in vmInfo.RecreateOrReload(Context.Engine).ToAsync()
                select vmInfoRecreated).ToEither();
        }
    }
}