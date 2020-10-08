using System;
using System.IO;
using System.Threading.Tasks;
using Haipa.CloudInit.ConfigDrive.Generator;
using Haipa.CloudInit.ConfigDrive.NoCloud;
using Haipa.CloudInit.ConfigDrive.Processing;
using Haipa.VmConfig;
using Haipa.VmManagement.Data.Core;
using Haipa.VmManagement.Data.Full;
using LanguageExt;
using Newtonsoft.Json.Linq;

namespace Haipa.VmManagement.Converging
{
    public class ConvergeCloudInitDisk : ConvergeTaskBase
    {
        public ConvergeCloudInitDisk(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var remove = await RemoveConfigDriveDisk(vmInfo).BindAsync(u => vmInfo.RecreateOrReload(Context.Engine)).ConfigureAwait(false);


            if (Context.Config.Provisioning.Method == ProvisioningMethod.None || remove.IsLeft)
            {
                return remove;
            }

            return await Context.StorageSettings.StorageIdentifier.MatchAsync(
                Some: async storageIdentifier =>
                {
                    var configDriveIsoPath = Path.Combine(Context.StorageSettings.VMPath, storageIdentifier, "configdrive.iso");

                    await Context.ReportProgress("Updating configdrive disk").ConfigureAwait(false);

                    await from _ in CreateConfigDriveDirectory(Context.StorageSettings.VMPath).AsTask()
                        select _;

                    GenerateConfigDriveDisk(configDriveIsoPath, Context.Config.Provisioning.Hostname,
                        Context.Config.Provisioning.UserData);

                    return await InsertConfigDriveDisk(configDriveIsoPath, vmInfo);

                },
                None: () => Context.ReportProgress("Missing storage identifier - cannot generate cloud init disk.")
                    .ToUnit().MapAsync(u => Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither())
            );


        }


        private static void GenerateConfigDriveDisk(string configDriveIsoPath,
            string hostname,
            JObject userData)
        {
            try
            {
                GeneratorBuilder.Init()
                    .NoCloud(new NoCloudConfigDriveMetaData(hostname))
                    .SwapFile()
                    .UserData(userData)
                    .Processing()
                    .Image().ImageFile(configDriveIsoPath)
                    .Generate();
            }
            catch (Exception ex)
            {
                return;
            }

            return;
        }


        private static Either<PowershellFailure, Unit> CreateConfigDriveDirectory(string configDrivePath)
        {
            if (Directory.Exists(configDrivePath)) return Unit.Default;

            var tryResult = Prelude.Try(Directory.CreateDirectory(configDrivePath)).Try();

            if (tryResult.IsFaulted)
                return new PowershellFailure { Message = $"Failed to create directory {configDrivePath}" };

            return Unit.Default;
        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EjectConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return ConvergeHelpers.FindAndApply(vmInfo, l => l.DVDDrives,
                    drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0,
                    drive => Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Set-VMDvdDrive")
                        .AddParameter("VMDvdDrive", drive.PsObject)
                        .AddParameter("Path", null)))

                .Map(list => list.Lefts().HeadOrNone()).MatchAsync(
                    None: () => vmInfo.RecreateOrReload(Context.Engine),
                    Some: l => Prelude.LeftAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(l).ToEither());

        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> RemoveConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return ConvergeHelpers.FindAndApply(vmInfo, l => l.DVDDrives,
                    drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0,
                    drive => Context.Engine.RunAsync(PsCommandBuilder.Create()
                        .AddCommand("Remove-VMDvdDrive")
                        .AddParameter("VMDvdDrive", drive.PsObject)))
                .Map(list => list.Lefts().HeadOrNone()).MatchAsync(
                    None: () => vmInfo.RecreateOrReload(Context.Engine),
                    Some: l => Prelude.LeftAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(l).ToEither());

        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> InsertConfigDriveDisk(
            string configDriveIsoPath,
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {

            return
                from dvdDrive in ConvergeHelpers.GetOrCreateInfoAsync(vmInfo,
                    l => l.DVDDrives,
                    drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0,
                    () => Context.Engine.GetObjectsAsync<DvdDriveInfo>(
                        PsCommandBuilder.Create().AddCommand("Add-VMDvdDrive")
                            .AddParameter("VM", vmInfo.PsObject)
                            .AddParameter("ControllerNumber", 0)
                            .AddParameter("ControllerLocation", 63)
                            .AddParameter("PassThru"))
                )

                from _ in Context.Engine.RunAsync(PsCommandBuilder.Create()

                    .AddCommand("Set-VMDvdDrive")
                    .AddParameter("VMDvdDrive", dvdDrive.PsObject)
                    .AddParameter("Path", configDriveIsoPath))

                from vmInfoRecreated in vmInfo.RecreateOrReload(Context.Engine)
                select vmInfoRecreated;

        }
    }
}