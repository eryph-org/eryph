using System;
using System.Threading.Tasks;
using Eryph.VmManagement.Data.Full;
using LanguageExt;

namespace Eryph.VmManagement
{
    public interface IVirtualMachineInfoProvider
    {
        Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(string vmName);
        Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(Guid id);
        void SetInfo(TypedPsObject<VirtualMachineInfo> info);
        void ClearInfo(Guid id);
    }
}