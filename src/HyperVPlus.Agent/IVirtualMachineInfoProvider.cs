using System;
using System.Threading.Tasks;
using HyperVPlus.Agent.Management;
using HyperVPlus.Agent.Management.Data;
using HyperVPlus.VmManagement;
using LanguageExt;

namespace HyperVPlus.Agent
{
    internal interface IVirtualMachineInfoProvider
    {
        Task<Either<PowershellFailure,TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(string vmName);
        Task<Either<PowershellFailure, TypedPsObject<VirtualMachineInfo>>> GetInfoAsync(Guid id);
        void SetInfo(TypedPsObject<VirtualMachineInfo> info);
        void ClearInfo(Guid id);

    }

}