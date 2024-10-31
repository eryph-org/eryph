using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.Specification;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ModuleCore.Networks;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using Eryph.StateDb;
using Eryph.StateDb.Model;
using Eryph.StateDb.Specifications;
using Microsoft.AspNetCore.Mvc;
using VirtualNetwork = Eryph.StateDb.Model.VirtualNetwork;

namespace Eryph.Modules.ComputeApi.Handlers;

internal class GetVirtualNetworksConfigurationHandler(
    IStateStore stateStore)
    : IGetRequestHandler<Project, VirtualNetworkConfiguration>
{
    public async Task<ActionResult<VirtualNetworkConfiguration>> HandleGetRequest(
        Func<ISingleResultSpecification<Project>?> specificationFunc,
        CancellationToken cancellationToken)
    {
        var projectSpec = specificationFunc();
        if (projectSpec is null)
            return new NotFoundResult();

        var project = await stateStore.Read<Project>().GetBySpecAsync(projectSpec, cancellationToken);
        if (project is null)
            return new NotFoundResult();
            
        var networks = await stateStore.For<VirtualNetwork>().ListAsync(
            new VirtualNetworkSpecs.GetForProjectConfig(project.Id),
            cancellationToken);

        var projectConfig = networks.ToNetworksConfig(project.Name);

        var result = new VirtualNetworkConfiguration()
        {
            Configuration = ProjectNetworksConfigJsonSerializer.SerializeToElement(projectConfig),
        };

        return result;
    }
}