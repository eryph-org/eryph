using System;
using System.Threading.Tasks;
using Haipa.VmManagement.Data;
using Haipa.VmManagement.Data.Full;
using LanguageExt;

namespace Haipa.VmManagement
{
    public interface IVirtualMachineInfoProvider
    {
        Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(string vmName);
        Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(Guid id);
        void SetInfo(TypedPsObject<VirtualMachineInfo> info);
        void ClearInfo(Guid id);

    }

}