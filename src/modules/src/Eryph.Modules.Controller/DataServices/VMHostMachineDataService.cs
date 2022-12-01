using System;
using System.Linq;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using LanguageExt;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VMHostMachineDataService : IVMHostMachineDataService
    {
        private readonly IStateStoreRepository<VirtualCatletHost> _repository;

        public VMHostMachineDataService(IStateStoreRepository<VirtualCatletHost> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VirtualCatletHost>> GetVMHost(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res;
        }

        public async Task<VirtualCatletHost> AddNewVMHost(VirtualCatletHost vmHostMachine)
        {
            if (vmHostMachine.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualCatletHost.Id)} is missing", nameof(vmHostMachine));


            var res = await _repository.AddAsync(vmHostMachine);
            return res;
        }

        
        public async Task<Option<VirtualCatletHost>> GetVMHostByHardwareId(string hardwareId)
        {
            return await _repository.GetBySpecAsync(new VMHostMachineSpecs.GetByHardwareId(hardwareId));
        }

        public async Task<Option<VirtualCatletHost>> GetVMHostByAgentName(string agentName)
        {
            return (await _repository.ListAsync(new ResourceSpecs<VirtualCatletHost>.GetByName(agentName))).FirstOrDefault();
        }
    }
}