using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using JetBrains.Annotations;
using LanguageExt;
using Rebus.Retry.FailFast;

namespace Eryph.Modules.Controller.DataServices
{
    internal class VirtualMachineDataService : IVirtualMachineDataService
    {
        private readonly IVirtualMachineMetadataService _metadataService;
        private readonly IStateStoreRepository<Catlet> _repository;
        private readonly IStateStore _stateStore;

        public VirtualMachineDataService(IStateStoreRepository<Catlet> repository,
            IVirtualMachineMetadataService metadataService, IStateStore stateStore)
        {
            _repository = repository;
            _metadataService = metadataService;
            _stateStore = stateStore;
        }

        public async Task<Option<Catlet>> GetByVMId(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new CatletSpecs.GetByVMId(id));
            return res!;
        }

        public async Task<Option<Catlet>> GetVM(Guid id)
        {
            var res = await _repository.GetBySpecAsync(new CatletSpecs.GetById(id));
            return res!;
        }

        public async Task<Catlet> AddNewVM(
            Catlet catlet,
            CatletMetadataContent? metadataContent,
            bool secretDataHidden = false)
        {
            if (catlet.ProjectId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.ProjectId)} is missing", nameof(catlet));

            if (catlet.Id == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.Id)} is missing", nameof(catlet));

            if (catlet.MetadataId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.MetadataId)} is missing", nameof(catlet));

            if (catlet.VMId == Guid.Empty)
                throw new ArgumentException($"{nameof(Catlet.VMId)} is missing", nameof(catlet));

            var metadata = new CatletMetadata
            {
                Id = catlet.MetadataId,
                CatletId = catlet.Id,
                VmId = catlet.VMId,
                Metadata = metadataContent,
                IsDeprecated = false,
                SecretDataHidden = false,
            };
            await _metadataService.AddMetadata(metadata);

            return await _repository.AddAsync(catlet);
        }

        public async Task<Unit> RemoveVM(Guid id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return Unit.Default;

            // remove floating port references
            var catletPorts = await _stateStore.For<CatletNetworkPort>().ListAsync(
                new CatletNetworkPortSpecs.GetByCatletMetadataId(entity.MetadataId));

            foreach (var catletPort in catletPorts)
            {
                await _stateStore.LoadPropertyAsync(catletPort, np => np.FloatingPort);
            }

            await _stateStore.For<FloatingNetworkPort>().DeleteRangeAsync(
                catletPorts.Where(x => x.FloatingPort != null).Select(x => x.FloatingPort));

            await _repository.DeleteAsync(entity);
            

            if (entity.MetadataId != Guid.Empty)
            {
                await _metadataService.RemoveMetadata(entity.MetadataId);
            }

            return Unit.Default;
        }

        public async Task<IEnumerable<Catlet>> GetAll()
        {
            return await _repository.ListAsync();
        }
    }
}