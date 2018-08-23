using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using HyperVPlus.VmManagement;
using HyperVPlus.VmManagement.Data;
using LanguageExt;

using static LanguageExt.Prelude;

namespace HyperVPlus.Agent
{
    internal class VirtualMachineInfoProvider : IVirtualMachineInfoProvider
    {

        private readonly IPowershellEngine _engine;
        private readonly ConcurrentDictionary<Guid, TypedPsObject<VirtualMachineInfo>> _buffer = new ConcurrentDictionary<Guid, TypedPsObject<VirtualMachineInfo>>();

        public VirtualMachineInfoProvider(IPowershellEngine engine)
        {
            _engine = engine;
        }

        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(string vmName) => 
            (await _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                .AddCommand("get-vm").AddArgument(vmName)).ConfigureAwait(false)).Bind(
                vmList =>
                {
                    var vmArray = vmList.ToArray();

                    if (vmArray.Length  > 1)
                        return Left<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(new PowershellFailure{ Message =  $"VM name '{vmName}' is not unique." });

                    var vmInfo = vmArray.SingleOrDefault();

                    SetInfo(vmInfo);

                    return Right<PowershellFailure,TypedPsObject<VirtualMachineInfo>>(vmInfo);

                });

        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(Guid id)
        {
            if (_buffer.TryGetValue(id, out var vmInfo))
                return vmInfo;

           return (await _engine.GetObjectAsync<VirtualMachineInfo>(
                PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", id)).ConfigureAwait(false)).Map(newVmInfo =>
            {
                SetInfo(newVmInfo);
                return newVmInfo;

            });
            
        }

        public void SetInfo(TypedPsObject<VirtualMachineInfo> info)
        {
            if (info == null)
                return;

            if (_buffer.ContainsKey(info.Value.Id))
                _buffer.TryRemove(info.Value.Id, out _);

            _buffer.TryAdd(info.Value.Id, info);

        }

        public void ClearInfo(Guid id)
        {
            if (_buffer.ContainsKey(id))
                return;

            _buffer.TryRemove(id, out _);
        }
    }
}