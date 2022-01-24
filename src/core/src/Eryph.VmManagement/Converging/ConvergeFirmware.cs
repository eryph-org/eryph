using System.Threading.Tasks;
using Eryph.VmManagement.Data;
using Eryph.VmManagement.Data.Core;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.VmManagement.Converging
{
    public class ConvergeFirmware : ConvergeTaskBase
    {
        public ConvergeFirmware(ConvergeContext context) : base(context)
        {
        }

        public override async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> Converge(
            TypedPsObject<VirtualMachineInfo> vmInfo)
        {
            if (vmInfo.Value.Generation < 2)
                return vmInfo;

            return await Context.Engine.GetObjectsAsync<VMFirmwareInfo>(PsCommandBuilder.Create()
                    .AddCommand("Get-VMFirmware")
                    .AddParameter("VM", vmInfo.PsObject))
                .BindAsync(firmwareInfoSeq =>
                    firmwareInfoSeq.HeadOrNone()
                        .Match<Either<PowershellFailure, TypedPsObject<VMFirmwareInfo>>>(
                            None: () => new PowershellFailure {Message = "Failed to get VM Firmware"},
                            Some: s => s
                        ))
                .BindAsync(async firmwareInfo =>
                    {
                        if (firmwareInfo.Value.SecureBoot != OnOffState.Off)
                        {
                            await Context.ReportProgress($"Configure VM Firmware - Secure Boot: {OnOffState.Off}")
                                .ConfigureAwait(false);


                            var res = await Context.Engine.RunAsync(PsCommandBuilder.Create()
                                .AddCommand("Set-VMFirmware")
                                .AddParameter("VM", vmInfo.PsObject)
                                .AddParameter("EnableSecureBoot", OnOffState.Off)).BindAsync(
                                _ => vmInfo.RecreateOrReload(Context.Engine)
                            ).ConfigureAwait(false);
                            return res;
                        }

                        return await vmInfo.RecreateOrReload(Context.Engine).ConfigureAwait(false);
                    }
                ).ConfigureAwait(false);
        }
    }
}