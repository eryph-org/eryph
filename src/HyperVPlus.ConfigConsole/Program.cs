using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HyperVPlus.VmConfig;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using LanguageExt;

using static LanguageExt.Prelude;
// ReSharper disable ArgumentsStyleAnonymousFunction

namespace HyperVPlus.ConfigConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using (var engine = new PowershellEngine())
            {

                var config = new VirtualMachineConfig
                {
                    Name = "basic11",
                    Hostname = "test",
                    Cpu = new VirtualMachineCpuConfig {Count = 2},
                    Memory = new VirtualMachineMemoryConfig() {Maximum = 2048, Minimum = 1024, Startup = 2048},
                    Disks = new List<VirtualMachineDiskConfig>
                    {
                        new VirtualMachineDiskConfig{ Name="os", Size=10240, Template = @"T:\openstack\ubuntu-xenial.vhdx"}
                    },
                    Path = @"T:\openstack\vms"
                };

                Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>> BindableEnsureUnique(
                    Seq<TypedPsObject<VirtualMachineInfo>> list) => EnsureUnique(list, config.Name);

                Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> BindableTaskEnsureCreated(
                    Seq<TypedPsObject<VirtualMachineInfo>> list) => EnsureCreated(list, config, engine);


                //Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> BindableConvergeVm(
                //    TypedPsObject<VirtualMachineInfo> vmInfo) => ConvergeVm(vmInfo, config, engine);


                var result = await GetVmInfo(config.Name, engine)
                    .BindAsync(BindableEnsureUnique)
                    .BindAsync(BindableTaskEnsureCreated)
                    .BindAsync(vmInfo => ConvergeVm(vmInfo, config, engine)).ConfigureAwait(false);

                await result.MatchAsync(
                    LeftAsync: HandleError,
                    Right: r => r
                ).ConfigureAwait(false);

            }
        }


        private static async Task<Either<PowershellFailure,Unit>> ConvergeVm(TypedPsObject<VirtualMachineInfo> vmInfo, VirtualMachineConfig config, IPowershellEngine engine)
        {
            var result = await Converge.Firmware(vmInfo, config, engine, ProgressMessage)
                .BindAsync(info => Converge.Cpu(info, config.Cpu, engine, ProgressMessage))
                .BindAsync(info => Converge.Disks(info, config.Disks?.ToSeq(), config, engine, ProgressMessage))

                .BindAsync(info => Converge.CloudInit(
                    info, config.Path, config.Hostname, config.Provisioning?.UserData, engine, ProgressMessage)).ConfigureAwait(false);

            //await Converge.Definition(engine, vmInfo, config, ProgressMessage).ConfigureAwait(false);

            config.Networks?.Iter(async (network) =>
                await Converge.Network(engine, vmInfo, network, config, ProgressMessage)
                    .ConfigureAwait(false));

            //await ProgressMessage("Generate Virtual Machine provisioning disk").ConfigureAwait(false);

            //await Converge.CloudInit(
            //    engine, config.Path,
            //    config.Hostname,
            //    config.Provisioning.UserData,
            //    vmInfo).ConfigureAwait(false);

            //}).ConfigureAwait(false);

            await ProgressMessage("Converged").ConfigureAwait(false);

            return result;
        }

        private static Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>> EnsureUnique(Seq<TypedPsObject<VirtualMachineInfo>> list, string vmName)
        {
            
            if(list.Count > 1)
                return Left(new PowershellFailure { Message = $"VM name '{vmName}' is not unique." });

            return Right(list);
        }

        private static Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> EnsureCreated(Seq<TypedPsObject<VirtualMachineInfo>> list, VirtualMachineConfig config, IPowershellEngine engine)
        {
            return list.HeadOrNone().MatchAsync(
                None: () => Converge.CreateVirtualMachine(engine, config.Name,
                    config.Path,
                    config.Memory.Startup),
                Some: s => s
            );

        }

        private static Task<Either<PowershellFailure, Seq<TypedPsObject<VirtualMachineInfo>>>> GetVmInfo(string vmName,
            IPowershellEngine engine)
        {
            return engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                .AddCommand("get-vm").AddArgument(vmName)
                //this a bit dangerous, because there may be other errors causing the 
                //command to fail. However there seems to be no other way except parsing error response
                .AddParameter("ErrorAction", "SilentlyContinue")
            );
        }

        private static async Task<Unit> HandleError(PowershellFailure failure)
        {
            return Unit.Default;
        }

        static async Task<Unit> ProgressMessage(string message)
        {
           Console.WriteLine(message);
            return Unit.Default;
        }
    }
}
