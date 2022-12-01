using System;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVMHostMachineDataService
{
    Task<Option<VirtualCatletHost>> GetVMHost(Guid id);
    Task<VirtualCatletHost> AddNewVMHost(VirtualCatletHost vmHostMachine);
    Task<Option<VirtualCatletHost>> GetVMHostByHardwareId(string hardwareId);
    Task<Option<VirtualCatletHost>> GetVMHostByAgentName(string agentName);

}