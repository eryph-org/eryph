using System;
using System.Threading.Tasks;
using Haipa.StateDb;
using Haipa.StateDb.Model;
using LanguageExt;

namespace Haipa.Modules.Controller
{
    internal interface IVirtualMachineDataService
    {
        Task<Option<VirtualMachine>> GetVM(Guid id);
    }

    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IStateStoreRepository<VirtualMachine> _repository;


        public VirtualMachineDataService(IStateStoreRepository<VirtualMachine> repository)
        {
            _repository = repository;
        }

        public async Task<Option<VirtualMachine>> GetVM(Guid id)
        {
           var res = await _repository.GetByIdAsync(id);
           return res;
        }

    }
}