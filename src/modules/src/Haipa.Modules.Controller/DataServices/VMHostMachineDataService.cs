using System;
using System.Linq;
using System.Threading.Tasks;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using Haipa.StateDb.Specifications;
using LanguageExt;

namespace Haipa.Modules.Controller.DataServices
{
    public interface IVMHostMachineDataService
    {
        Task<Option<VMHostMachine>> GetVMHost(Guid id);
        Task<VMHostMachine> AddNewVMHost(VMHostMachine vmHostMachine);
        Task<Option<VMHostMachine>> GetVMHostByHardwareId(string hardwareId);
        Task<Option<VMHostMachine>> GetVMHostByAgentName(string agentName);

    }

    internal class VMHostMachineDataService : IVMHostMachineDataService
    {
        private readonly IStateStoreRepository<VMHostMachine> _repository;

        public VMHostMachineDataService(IStateStoreRepository<VMHostMachine> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VMHostMachine>> GetVMHost(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res;
        }

        public async Task<VMHostMachine> AddNewVMHost(VMHostMachine vmHostMachine)
        {
            if (vmHostMachine.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VMHostMachine.Id)} is missing", nameof(vmHostMachine));


            var res = await _repository.AddAsync(vmHostMachine);
            return res;
        }

        
        public async Task<Option<VMHostMachine>> GetVMHostByHardwareId(string hardwareId)
        {
            return await _repository.GetBySpecAsync(new VMHostMachineSpecs.GetByHardwareId(hardwareId));
        }

        public async Task<Option<VMHostMachine>> GetVMHostByAgentName(string agentName)
        {
            return (await _repository.ListAsync(new ResourceSpecs<VMHostMachine>.GetByName(agentName))).FirstOrDefault();
        }
    }
}