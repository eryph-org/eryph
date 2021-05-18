using System;
using System.Threading.Tasks;
using Haipa.Resources.Machines;
using Haipa.StateDb;
using JetBrains.Annotations;
using LanguageExt;
using Newtonsoft.Json;

namespace Haipa.Modules.Controller.DataServices
{
    public interface IVirtualMachineMetadataService
    {
        Task<Option<VirtualMachineMetadata>> GetMetadata(Guid id);
        Task<Unit> SaveMetadata(VirtualMachineMetadata metadata);
    }

    internal class VirtualMachineMetadataService : IVirtualMachineMetadataService
    {
        private readonly IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> _repository;

        public VirtualMachineMetadataService(IStateStoreRepository<StateDb.Model.VirtualMachineMetadata> repository)
        {
            _repository = repository;
        }

        public Task<Option<VirtualMachineMetadata>> GetMetadata(Guid id)
        {
            return _repository.GetByIdAsync(id).Map(DeserializeMetadataEntity);
        }

        public async Task<Unit> SaveMetadata(VirtualMachineMetadata metadata)
        {
            var entity = await _repository.GetByIdAsync(metadata.Id);
            if (entity == null)
            {
                await _repository.AddAsync(new StateDb.Model.VirtualMachineMetadata
                {
                    Id = metadata.Id,
                    Metadata = JsonConvert.SerializeObject(metadata)
                });

                return Unit.Default;
            }

            entity.Metadata = JsonConvert.SerializeObject(metadata);
            await _repository.UpdateAsync(entity);
            return Unit.Default;
        }

        private static Option<VirtualMachineMetadata> DeserializeMetadataEntity(
            [CanBeNull] StateDb.Model.VirtualMachineMetadata metadataEntity)
        {
            return JsonConvert.DeserializeObject<VirtualMachineMetadata>(metadataEntity.Metadata);
        }
    }
}