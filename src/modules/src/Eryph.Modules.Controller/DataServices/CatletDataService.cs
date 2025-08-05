using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;

namespace Eryph.Modules.Controller.DataServices;

internal class CatletDataService(
    IStateStoreRepository<Catlet> repository,
    ICatletMetadataService metadataService,
    IStateStore stateStore)
    : ICatletDataService
{
    public async Task<Catlet?> GetByVmId(Guid id)
    {
        return await repository.GetBySpecAsync(new CatletSpecs.GetByVmId(id));
    }

    public async Task<Catlet?> Get(Guid id)
    {
        return await repository.GetBySpecAsync(new CatletSpecs.GetById(id));
    }

    public async Task Add(Catlet catlet)
    {
        if (catlet.ProjectId == Guid.Empty)
            throw new ArgumentException($"{nameof(Catlet.ProjectId)} is missing", nameof(catlet));

        if (catlet.Id == Guid.Empty)
            throw new ArgumentException($"{nameof(Catlet.Id)} is missing", nameof(catlet));

        if (catlet.MetadataId == Guid.Empty)
            throw new ArgumentException($"{nameof(Catlet.MetadataId)} is missing", nameof(catlet));

        if (catlet.VmId == Guid.Empty)
            throw new ArgumentException($"{nameof(Catlet.VmId)} is missing", nameof(catlet));

        await repository.AddAsync(catlet);
    }

    public async Task Remove(Guid id)
    {
        var entity = await repository.GetByIdAsync(id);
        if (entity is null)
            return;

        // Remove floating ports
        var catletPorts = await stateStore.For<CatletNetworkPort>().ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(entity.MetadataId));

        await stateStore.For<FloatingNetworkPort>().DeleteRangeAsync(
            catletPorts.Where(cp => cp.FloatingPort is not null).Select(cp => cp.FloatingPort!));

        await repository.DeleteAsync(entity);
        await metadataService.RemoveMetadata(entity.MetadataId);
    }

    public async Task<IReadOnlyList<Guid>> GetAllVmIds(string agentName)
    {
        return await stateStore.Read<Catlet>().ListAsync(
            new CatletSpecs.GetAllVmIds(agentName));
    }
}
