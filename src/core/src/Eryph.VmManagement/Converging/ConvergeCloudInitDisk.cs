using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Dbosoft.CloudInit.ConfigDrive.Generator;
using Dbosoft.CloudInit.ConfigDrive.NoCloud;
using Dbosoft.CloudInit.ConfigDrive.Processing;
using Eryph.Resources.Machines.Config;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;
using Newtonsoft.Json.Linq;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeCloudInitDisk : ConvergeTaskBase
    {
        public ConvergeCloudInitDisk(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            var remove = await RemoveConfigDriveDisk(vmInfo).BindAsync(u => vmInfo.RecreateOrReload(Context.Engine))
                .ConfigureAwait(false);


            return await Context.StorageSettings.StorageIdentifier.MatchAsync(
                async storageIdentifier =>
                {
                    var configDriveIsoPath = Path.Combine(Context.StorageSettings.VMPath, storageIdentifier,
                        "configdrive.iso");

                    await Context.ReportProgress("Updating configdrive disk").ConfigureAwait(false);

                    await (from _ in CreateConfigDriveDirectory(Context.StorageSettings.VMPath).AsTask()
                        select _);

                    var userData = ReplaceVariables(Context.Config.Provisioning.UserData);

                    GenerateConfigDriveDisk(configDriveIsoPath,
                    Context.Config.Provisioning.Method == ProvisioningMethod.None,
                        Context.Config.Provisioning.Hostname,
                        userData);

                    return await InsertConfigDriveDisk(configDriveIsoPath, vmInfo);
                },
                () => Context.ReportProgress("Missing storage identifier - cannot generate cloud init disk.")
                    .ToUnit().MapAsync(u =>
                        Prelude.RightAsync<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(vmInfo).ToEither())
            );
        }

        private JObject ReplaceVariables(JObject userData)
        {
            if (userData == null)
                return null;

            var jsonBuilder = new StringBuilder(userData.ToString());
            jsonBuilder.Replace("{{machineId}}", Context.Metadata.MachineId.ToString());
            jsonBuilder.Replace("{{vmId}}", Context.Metadata.VMId.ToString());

            return JObject.Parse(jsonBuilder.ToString());
        }


        private static void GenerateConfigDriveDisk(string configDriveIsoPath,
            bool minimalDrive, 
            string hostname,
            JObject userData)
        {
            try
            {
                var builder = GeneratorBuilder.Init()
                    .NoCloud(new NoCloudConfigDriveMetaData(hostname));

                builder.SwapFile();

                if(!minimalDrive)    
                    builder.UserData(userData);


                builder.Processing()
                    .Image().ImageFile(configDriveIsoPath)
                    .Generate();
            }
            catch (Exception ex)
            {
                return;
            }
        }


        private static Either<PowershellFailure, Unit> CreateConfigDriveDirectory(string configDrivePath)
        {
            if (Directory.Exists(configDrivePath)) return Unit.Default;

            var tryResult = Prelude.Try(Directory.CreateDirectory(configDrivePath)).Try();

            if (tryResult.IsFaulted)
                return new PowershellFailure {Message = $"Failed to create directory {configDrivePath}"};

            return Unit.Default;
        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EjectConfigDriveDisk(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            return ConvergeHelpers.FindAndApply(vmInfo, l => l.DVDDrives,
                    device =>
                    {
                        var drive = device.Cast<DvdDriveInfo>();
                        return drive.Value.ControllerLocation == 63 && drive.Value.ControllerNumber == 0;
                    },
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
                    device =>
                        device.Cast<DvdDriveInfo>().Map(drive => drive.ControllerLocation == 63 && drive.ControllerNumber == 0),
                    drive =>
                    {
                        return Context.Engine.RunAsync(PsCommandBuilder.Create()
                            .AddCommand("Remove-VMDvdDrive")
                            .AddParameter("VMDvdDrive", drive.PsObject));
                    })
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
                    device => device.Cast<DvdDriveInfo>()
                        .Map(drive =>drive.ControllerLocation == 63 && drive.ControllerNumber == 0),
                    () => Context.Engine.GetObjectsAsync<VirtualMachineDeviceInfo>(
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