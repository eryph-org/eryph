using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Eryph.ConfigModel;
using Eryph.Core.Genetics;
using Eryph.Modules.Controller.Serializers;
using Eryph.Resources.Machines;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using JetBrains.Annotations;
using LanguageExt;

using static LanguageExt.Prelude;
using DbCatletMetadata = Eryph.StateDb.Model.CatletMetadata;

namespace Eryph.Modules.Controller.DataServices;

internal class VirtualMachineMetadataService(
    IStateStoreRepository<DbCatletMetadata> repository)
    : IVirtualMachineMetadataService
{
    public async Task<CatletMetadata?> GetMetadata(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // TODO use spec to force detached entity. The metadata should not be changed normally.
        return await repository.GetByIdAsync(id, cancellationToken);
    }

    public async Task AddMetadata(
        CatletMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        await repository.AddAsync(metadata, cancellationToken);

        metadata.Genes = (metadata.Metadata?.PinnedGenes.Keys).ToSeq()
            .Map(g => new CatletMetadataGene
            {
                MetadataId = metadata.Id,
                GeneSet = g.Id.GeneSet.Value,
                Name = g.Id.GeneName.Value,
                Architecture = g.Architecture.Value,
            })
            .ToList();
    }

    public async Task MarkSecretDataHidden(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var metadata = await repository.GetByIdAsync(id, cancellationToken);
        if (metadata is null)
            throw new InvalidOperationException($"The catlet metadata {id} does not exist.");
        
        metadata.SecretDataHidden = true;
        await repository.UpdateAsync(metadata, cancellationToken);
    }

    public async Task RemoveMetadata(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await repository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return;

        await repository.DeleteAsync(entity, cancellationToken);
    }
}
