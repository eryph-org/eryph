using System;
using System.Threading.Tasks;
using Eryph.StateDb.Model;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices;

public interface IVMHostMachineDataService
{
    Task<Option<CatletFarm>> GetVMHost(Guid id);
    Task<CatletFarm> AddNewVMHost(CatletFarm vmHostMachine);
    Task<Option<CatletFarm>> GetVMHostByHardwareId(string hardwareId);
    Task<Option<CatletFarm>> GetVMHostByAgentName(string agentName);

}