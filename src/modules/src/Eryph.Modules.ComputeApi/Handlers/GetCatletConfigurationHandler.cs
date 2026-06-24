using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.CatletManagement;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using Catlet = Eryph.StateDb.Model.Catlet;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetCatletConfigurationHandler(
    IStateStore stateStore)
    : IGetRequestHandler<Catlet, CatletConfiguration>
{
    public async Task<ActionResult<CatletConfiguration>> HandleGetRequest(
        Func<ISingleResultSpecification<Catlet>?> specificationFunc,
        CancellationToken cancellationToken)
    {
        var catletSpec = specificationFunc();
        if (catletSpec is null)
            return new NotFoundResult();

        var repo = stateStore.Read<Catlet>();
        var catletIdResult = await repo.GetBySpecAsync(catletSpec, cancellationToken);

        if (catletIdResult == null)
            return new NotFoundResult();

        var catlet = await repo.GetBySpecAsync(
            new CatletSpecs.GetForConfig(catletIdResult.Id),
            cancellationToken);

        if (catlet == null)
            return new NotFoundResult();

        var networkPorts = await stateStore.Read<CatletNetworkPort>().ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId),
            cancellationToken);

        var metadata = await stateStore.Read<CatletMetadata>().GetByIdAsync(
            catlet.MetadataId, cancellationToken);
        switch (metadata)
        {
            case null:
                throw new InvalidOperationException(
                    $"The metadata for catlet {catletIdResult.Id} is missing.");
            // Only deprecated (v0.4) catlets may have incomplete metadata. For modern
            // catlets a null Metadata is state corruption and must surface as an error
            // rather than silently produce a partial config.
            case { IsDeprecated: false, Metadata: null }:
                throw new InvalidOperationException(
                    $"The metadata for catlet {catletIdResult.Id} is incomplete.");
        }

        // Deprecated catlets carry only partial metadata salvaged from v0.4 records
        // (typically just Parent and Architecture). Variables and fodder were never
        // recovered, so we omit them rather than emit defaults that pretend completeness.
        var originalConfig = metadata.Metadata?.Config ?? new CatletConfig();

        var config = CatletConfigGenerator.Generate(catlet, networkPorts.ToSeq().Strict(), originalConfig);

        // Variables and fodder cannot change after the first deployment. Hence, we
        // take them from metadata.
        config.Variables = originalConfig.Variables;
        config.Fodder = originalConfig.Fodder;

        var sanitizedConfig = CatletConfigRedactor.RedactSecrets(config);
        var trimmedConfig = CatletConfigNormalizer.Trim(sanitizedConfig);

        var result = new CatletConfiguration
        {
            Configuration = CatletConfigJsonSerializer.SerializeToElement(trimmedConfig),
        };

        return result;
    }
}
