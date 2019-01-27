using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Haipa.VmManagement.Data;
using LanguageExt;

namespace Haipa.VmManagement
{
    public class VirtualMachineInfoProvider : IVirtualMachineInfoProvider
    {

        private readonly IPowershellEngine _engine;
        private readonly ConcurrentDictionary<Guid, TypedPsObject<VirtualMachineInfo>> _buffer = new ConcurrentDictionary<Guid, TypedPsObject<VirtualMachineInfo>>();

        public VirtualMachineInfoProvider(IPowershellEngine engine)
        {
            _engine = engine;
        }

        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(string vmName) => 
            (await _engine.GetObjectsAsync<VirtualMachineInfo>(PsCommandBuilder.Create()
                .AddCommand("get-vm").AddArgument(vmName)).ConfigureAwait(false))
            .Apply(
                queryResult =>
                {
                    return queryResult.Match(
                        Left: l => l,
                        Right: r =>
                        {
                            var vmArray = r.ToArray();

                            if (vmArray.Length > 1)
                                return Prelude.Left<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(
                                    new PowershellFailure { Message = $"VM name '{vmName}' is not unique." });

                            if (vmArray.Length == 0)
                                return Prelude.Left<PowershellFailure, TypedPsObject<VirtualMachineInfo>>(
                                    new PowershellFailure { Message = $"VM name '{vmName}' not found" });

                            var vmInfo = vmArray[0];

                            SetInfo(vmInfo);
                            return vmInfo;
                        }
                    );
                });

        public async Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(Guid id)
        {
            if (_buffer.TryGetValue(id, out var vmInfo))
                return vmInfo;

           return (await _engine.GetObjectsAsync<VirtualMachineInfo>(
                PsCommandBuilder.Create()
                    .AddCommand("get-vm").AddParameter("Id", id)).ConfigureAwait(false)).Map(newVmInfo =>
            {
                SetInfo(newVmInfo.Head);
                return newVmInfo.Head;

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