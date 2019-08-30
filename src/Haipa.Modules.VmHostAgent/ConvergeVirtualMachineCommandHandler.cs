using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Haipa.Messages;
using Haipa.VmConfig;
using Haipa.VmManagement;
using Haipa.VmManagement.Data;
using JetBrains.Annotations;
using LanguageExt;
using Rebus;
using Rebus.Bus;
using Rebus.Handlers;
using Rebus.Transport;

// ReSharper disable ArgumentsStyleAnonymousFunction

namespace Haipa.Modules.VmHostAgent
{
    [UsedImplicitly]
    internal class ConvergeTaskRequestedEventHandler : IHandleMessages<AcceptedOperation<ConvergeVirtualMachineCommand>>
    {
        private readonly IPowershellEngine _engine;
        private readonly IBus _bus;
        private Guid _operationId;

        public ConvergeTaskRequestedEventHandler(
            IPowershellEngine engine,
            IBus bus)
        {
            _engine = engine;
            _bus = bus;
        }

        public Task Handle(AcceptedOperation<ConvergeVirtualMachineCommand> message)
        {
            var command = message.Command;
            var config = command.Config;
            _operationId = command.OperationId;

            var chain = 
                from normalizedVMConfig in Converge.NormalizeMachineConfig(config, _engine, ProgressMessage)
                from vmList in GetVmInfo(normalizedVMConfig.Id, _engine)
                from optionalVmInfo in EnsureUnique(vmList, normalizedVMConfig.Id)
                from storageSettings in DetectStorageSettings(optionalVmInfo, GetHostSettings())
                from vmInfoCreated in EnsureCreated(optionalVmInfo, config, storageSettings, _engine)
                from vmInfo in EnsureNameConsistent(vmInfoCreated, config, _engine)
                from _ in AttachToOperation(vmInfo, _bus, command.OperationId)
                from vmInfoConverged in ConvergeVm(vmInfo, config, storageSettings, _engine)
                select vmInfoConverged;

            return chain.ToAsync().MatchAsync(
                LeftAsync: HandleError,
                RightAsync: vmInfo2 => _bus.Send(new OperationCompletedEvent
                {
                    OperationId = command.OperationId,
                }).ToUnit());

        }

        private async Task<Unit> ProgressMessage(string message)
        {
            using (var scope = new RebusTransactionScope())
            {
                await _bus.Send(new ConvergeVirtualMachineProgressEvent
                {
                    OperationId = _operationId,
                    Message = message
                }).ConfigureAwait(false);

                // commit it like this
                await scope.CompleteAsync().ConfigureAwait(false);
            }
            return Unit.Default;

        }

        private Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> ConvergeVm(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig machineConfig, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            return
                from infoFirmware in Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                from infoCpu in Converge.Cpu(infoFirmware, machineConfig.VM.Cpu, engine, ProgressMessage)
                from infoDisks in Converge.Disks(infoCpu, machineConfig.VM.Disks.ToSeq(), machineConfig, storageSettings, engine, ProgressMessage)
                from infoNetworks in Converge.NetworkAdapters(infoDisks, machineConfig.VM.NetworkAdapters.ToSeq(), machineConfig, engine, ProgressMessage)
                from infoCloudInit in Converge.CloudInit(infoNetworks, storageSettings.VMPath,machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)
                select infoCloudInit;
                
                //Converge.Firmware(vmInfo, machineConfig, engine, ProgressMessage)
                //.BindAsync(info => Converge.Cpu(info, machineConfig.VMConfig.Cpu, engine, ProgressMessage))
                //.BindAsync(info => Converge.Disks(info, machineConfig.VMConfig.Disks?.ToSeq(), machineConfig, engine, ProgressMessage))

                //.BindAsync(info => Converge.CloudInit(
                //    info, machineConfig.VMConfig.Path, machineConfig.Provisioning.Hostname, machineConfig.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);
            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

        }

        private async Task<Either<PowershellFailure, Option<TypedPsObject<VirtualMachineInfo>>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string id)
        {
            if (list.Count > 1)
                return Prelude.Left(new PowershellFailure { Message = $"VM id '{id}' is not unique." });

            return Prelude.Right(list.HeadOrNone());
        }

        private Task<string> GenerateId()
        {
            return _bus.SendRequest<GenerateIdReply>(new GenerateIdCommand(), null,TimeSpan.FromMinutes(5)).Map(r => LongToString(r.GeneratedId, 36));
        }

        public const string DefaultDigits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public static string LongToString(BigInteger subject, int @base, string digits = DefaultDigits)
        {
            if (@base < 2) { throw (new ArgumentException("Base must not be less than 2", nameof(@base))); }
            if (digits.Length < @base) { throw (new ArgumentException("Not enough Digits for the base", nameof(digits))); }


            var result = new StringBuilder();
            var sign = 1;

            if (subject < 0) { subject *= sign = -1; }

            do
            {
                result.Insert(0, digits[(int)(subject % @base)]);
                subject /= @base;
            }
            while (subject > 0);

            if (sign == -1)
            {
                result.Insert(0, '-');
            }

            return (result.ToString());
        }

        public static BigInteger StringToLong(string subject, int @base, string digits = DefaultDigits)
        {
            if (subject == null) { throw (new ArgumentNullException(nameof(subject), "Subject must not be null")); }
            if (@base < 2) { throw (new ArgumentException("Base must not be less than 2", nameof(@base))); }
            if (digits.Length < @base) { throw (new ArgumentException("Not enough Digits for the base", nameof(digits))); }

            BigInteger result = 0;
            var sign = 0;

            var digitsUpper = digits.ToUpper();

            foreach (var ch in subject)
            {
                var offset = digits.IndexOf(ch);

                if ((offset == -1) || (offset >= @base))
                {
                    offset = digitsUpper.IndexOf(char.ToUpper(ch));
                }

                if ((offset != -1) && (offset < @base))
                {
                    result = result * @base + offset;

                    if (sign == 0)
                    {
                        sign = 1;
                    }
                }
                else
                {
                    if ((sign == 0) && (ch == '-'))
                    {
                        sign = -1;
                    }
                }
            }

            return (result * sign);
        }


        private Task<Either<PowershellFailure, VMStorageSettings>> DetectStorageSettings(
            Option<TypedPsObject<VirtualMachineInfo>> optionalVmInfo, HostSettings hostSettings) =>
            Prelude.Try(
                () => optionalVmInfo.MatchAsync(
                    None: () => GenerateId().Map(
                        id =>
                        {
                            return new VMStorageSettings
                            {
                                StorageIdentifier = id.ToString(),
                                VMPath = hostSettings.DefaultDataPath,
                                DefaultDiskPath = hostSettings.DefaultVirtualHardDiskPath,
                            };
                        }),

                    Some: (vmInfo) =>
                    {
                        return SplitVMPath(vmInfo.Value.Path).Match(
                            None: () => throw new Exception(
                                "Invalid vm path. For haipa all VMs have be in folders at least one folder after disk root"),
                            Some:
                            s => new VMStorageSettings
                            {
                                StorageIdentifier = s.DirectoryName,
                                VMPath = s.ParentPath,
                                DefaultDiskPath = hostSettings.DefaultVirtualHardDiskPath,
                            }).AsTask();

                    }).GetAwaiter().GetResult())
                ().Match(
                    Succ: r => r,
                    Fail: ex => Prelude.Left<PowershellFailure, VMStorageSettings>(new PowershellFailure{Message = ex.Message})).ToAsync().ToEither();


        private static Option<(string ParentPath, string DirectoryName)> SplitVMPath(string path)
        {
            var directoryInfo = new DirectoryInfo(path);

            if (directoryInfo.Parent == null)
                return Prelude.None;

            return (ParentPath: directoryInfo.Parent.FullName, DirectoryName: directoryInfo.Name);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Option<TypedPsObject<VirtualMachineInfo>> vmInfo, MachineConfig config, VMStorageSettings storageSettings, IPowershellEngine engine)
        {
            return vmInfo.MatchAsync(
                None: () => Converge.CreateVirtualMachine(engine, config.Name, storageSettings.StorageIdentifier,
                    storageSettings.VMPath,
                    config.VM.Memory.Startup),
                Some: s => s
            );

        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureNameConsistent(TypedPsObject<VirtualMachineInfo> vmInfo, MachineConfig config, IPowershellEngine engine)
        {
            return Prelude.Cond<(string currentName, string newName)>((names) =>
                    !string.IsNullOrWhiteSpace(names.newName) &&
                    !names.newName.Equals(names.currentName, StringComparison.InvariantCulture))((vmInfo.Value.Name,
                    config.Name))

                .MatchAsync(
                    None: () => vmInfo,
                    Some: (some) => Converge.RenameVirtualMachine(engine, vmInfo, config.Name));


        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> AttachToOperation(Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>> vmInfo, IBus bus, Guid operationId)
        {
            return vmInfo.MapAsync( async
                info =>
                {
                    await bus.Send(new AttachMachineToOperationCommand
                    {
                        OperationId = operationId,
                        AgentName = Environment.MachineName,
                        MachineId = info.Value.Id,
                    }).ConfigureAwait(false);
                    return info;
                }

            );

        }

        private static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(string id,
            IPowershellEngine engine) =>

            Prelude.Cond<string>((c) => !string.IsNullOrWhiteSpace(c))(id).MatchAsync(
                None:  () => Seq<TypedPsObject<VirtualMachineInfo>>.Empty,
                Some: (s) => engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", s)
                    //this a bit dangerous, because there may be other errors causing the 
                    //command to fail. However there seems to be no other way except parsing error response
                    .AddParameter("ErrorAction", "SilentlyContinue")
                ));

        private async Task<Unit> HandleError(PowershellFailure failure)
        {

            await _bus.Send(new OperationFailedEvent()
            {
                OperationId = _operationId,
                ErrorMessage = failure.Message,
            }).ConfigureAwait(false);

            return Unit.Default;
        }

        public HostSettings GetHostSettings()
        {

            var scope = new ManagementScope(@"\\.\root\virtualization\v2");
            var query = new ObjectQuery("select DefaultExternalDataRoot,DefaultVirtualHardDiskPath from Msvm_VirtualSystemManagementServiceSettingData");


            var searcher = new ManagementObjectSearcher (scope, query );
            var settingsCollection = searcher.Get ( );
            
            foreach (var hostSettings in settingsCollection)
            {
                return new HostSettings
                {
                    DefaultVirtualHardDiskPath = hostSettings.GetPropertyValue("DefaultVirtualHardDiskPath")?.ToString(),
                    DefaultDataPath = hostSettings.GetPropertyValue("DefaultExternalDataRoot")?.ToString(),
                };
            }

            throw new Exception("failed to query for hyper-v host settings");

        }

    }

}