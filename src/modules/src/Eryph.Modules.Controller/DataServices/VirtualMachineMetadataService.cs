using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;
using CatletMetadata = Eryph.Resources.Machines.CatletMetadata;

namespace Eryph.Modules.Controller.DataServices;

internal class VirtualMachineMetadataService : IVirtualMachineMetadataService
{
    private readonly IStateStoreRepository<StateDb.Model.CatletMetadata> _repository;

    public VirtualMachineMetadataService(
        IStateStoreRepository<StateDb.Model.CatletMetadata> repository)
    {
        _repository = repository;
    }

    public async Task<Option<CatletMetadata>> GetMetadata(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (id == Guid.Empty)
            return Option<CatletMetadata>.None;

        var entity = await _repository.GetByIdAsync(id, cancellationToken);

        if(entity == null)
            return Option<CatletMetadata>.None;

        return DeserializeMetadataEntity(entity);
    }

    public async Task<Unit> SaveMetadata(
        CatletMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(
            metadata.Id, cancellationToken);
        if (entity is null)
        {
            entity = new StateDb.Model.CatletMetadata
            {
                Id = metadata.Id,
                Metadata = JsonSerializer.Serialize(metadata)
            };
            await _repository.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity.Metadata = JsonSerializer.Serialize(metadata);
        }

        // TODO this churns through IDs
        entity.Genes = metadata.Fodder.ToSeq()
            .Append(Prelude.Optional(metadata.ParentConfig).ToSeq().Map(p => p.Fodder.ToSeq()))
            .Map(f => GeneIdentifier.NewOption(f.Source))
            .Somes()
            .Distinct()
            .Map(g => new CatletMetadataGene
            {
                MetadataId = metadata.Id,
                GeneSet = g.GeneSet.Value,
                GeneName = g.GeneName.Value,
            })
            .ToList();

        return Unit.Default;
    }

    public async Task<Unit> RemoveMetadata(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(
            id, cancellationToken);
        if(entity == null)
            return Unit.Default;

        await _repository.DeleteAsync(entity, cancellationToken);

        return Unit.Default;
    }

    private static Option<CatletMetadata> DeserializeMetadataEntity(
        [CanBeNull] StateDb.Model.CatletMetadata metadataEntity)
    {
        return JsonSerializer.Deserialize<CatletMetadata>(metadataEntity.Metadata);
    }
}
