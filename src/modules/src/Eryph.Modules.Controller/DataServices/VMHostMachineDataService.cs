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
        private readonly IStateStoreRepository<CatletFarm> _repository;

        public VMHostMachineDataService(IStateStoreRepository<CatletFarm> repository)
        {
            _repository = repository;
        }

        public async Task<Option<CatletFarm>> GetVMHost(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res;
        }

        public async Task<CatletFarm> AddNewVMHost(CatletFarm vmHostMachine)
        {
            if (vmHostMachine.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(CatletFarm.Id)} is missing", nameof(vmHostMachine));


            var res = await _repository.AddAsync(vmHostMachine);
            return res;
        }


        public async Task<Option<CatletFarm>> GetVMHostByAgentName(string agentName)
        {
            return (await _repository.ListAsync(new ResourceSpecs<CatletFarm>.GetByName(agentName))).FirstOrDefault();
        }
    }
}