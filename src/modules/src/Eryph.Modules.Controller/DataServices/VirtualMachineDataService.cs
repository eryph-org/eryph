using System;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using VirtualMachineMetadata = Eryph.Resources.Machines.VirtualMachineMetadata;

namespace Eryph.Modules.Controller.DataServices
{
    internal interface IVirtualMachineDataService
    {
        Task<Option<VirtualMachine>> GetVM(Guid id);
        Task<VirtualMachine> AddNewVM(VirtualMachine vm, VirtualMachineMetadata metadata);
    }

    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStoreRepository<VirtualMachine> _repository;

        public VirtualMachineDataService(IStateStoreRepository<VirtualMachine> repository,
            IVirtualMachineMetadataService metadataService)
        {
            _repository = repository;
            _metadataService = metadataService;
        }

        public async Task<Option<VirtualMachine>> GetVM(Guid id)
        {
            var res = await _repository.GetByIdAsync(id);
            return res;
        }

        public async Task<VirtualMachine> AddNewVM([NotNull] VirtualMachine vm,
            [NotNull] VirtualMachineMetadata metadata)
        {
            if (vm.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(VirtualMachine.Id)} is missing", nameof(vm));

            if (vm.VMId == null)
                throw new ArgumentException($"{nameof(VirtualMachine.VMId)} is missing", nameof(vm));

            if (metadata.Id == null)
                throw new ArgumentException($"{nameof(metadata.Id)} is missing", nameof(metadata));

            if (metadata.VMId != vm.VMId)
                throw new ArgumentException($"{nameof(metadata.VMId)} is invalid.", nameof(metadata));

            if (metadata.MachineId != vm.Id)
                throw new ArgumentException($"{nameof(metadata.MachineId)} is invalid.", nameof(metadata));


            await _metadataService.SaveMetadata(metadata);

            vm.MetadataId = metadata.Id;

            var res = await _repository.AddAsync(vm);
            return res;
        }
    }
}