using System;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVMHostMachineDataService
{
    Task<Option<VMHostMachine>> GetVMHost(Guid id);
    Task<VMHostMachine> AddNewVMHost(VMHostMachine vmHostMachine);
    Task<Option<VMHostMachine>> GetVMHostByHardwareId(string hardwareId);
    Task<Option<VMHostMachine>> GetVMHostByAgentName(string agentName);

}