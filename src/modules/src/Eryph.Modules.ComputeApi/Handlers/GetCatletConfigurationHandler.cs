using System;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.CatletManagement;
using Eryph.ConfigModel.Json;
using Eryph.Core.Genetics;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Catlet = Eryph.StateDb.Model.Catlet;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetCatletConfigurationHandler(
    IApiResultFactory resultFactory,
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
        var catletIdResult= await repo.GetBySpecAsync(catletSpec, cancellationToken);

        if (catletIdResult == null)
            return new NotFoundResult();

        var catlet = await repo.GetBySpecAsync(
            new CatletSpecs.GetForConfig(catletIdResult.Id),
            cancellationToken);

        if (catlet == null)
            return new NotFoundResult();

        if (catlet.IsDeprecated)
        {
            return resultFactory.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                detail: "The catlet is deprecated and cannot be managed.");
        }

        var networkPorts = await stateStore.Read<CatletNetworkPort>().ListAsync(
            new CatletNetworkPortSpecs.GetByCatletMetadataId(catlet.MetadataId),
            cancellationToken);

        var metadata = await stateStore.Read<CatletMetadata>().GetByIdAsync(
            catlet.MetadataId, cancellationToken);
        if (metadata is null || metadata.IsDeprecated || metadata.Metadata is null)
            throw new InvalidOperationException(
                $"The metadata for catlet {catletIdResult.Id} is missing or incomplete.");

        var config = CatletConfigGenerator.Generate(catlet, networkPorts.ToSeq().Strict(), metadata.Metadata.BuiltConfig);

        // Variables and fodder cannot change after the first deployment. Hence, we
        // take them from metadata.
        config.Variables = metadata.Metadata.BuiltConfig.Variables;
        config.Fodder = metadata.Metadata.BuiltConfig.Fodder;

        var sanitizedConfig = CatletConfigRedactor.RedactSecrets(config);
        var trimmedConfig = CatletConfigNormalizer.Trim(sanitizedConfig);

        var result = new CatletConfiguration
        {
            Configuration = CatletConfigJsonSerializer.SerializeToElement(trimmedConfig),
        };

        return result;
    }
}
