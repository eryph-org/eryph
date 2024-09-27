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

namespace Eryph.Modules.ComputeApi.Handlers
{
    internal class GetVirtualNetworksConfigurationHandler : IGetRequestHandler<Project, 
        VirtualNetworkConfiguration>
    {
        private readonly IStateStore _stateStore;

        public GetVirtualNetworksConfigurationHandler(IStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        public async Task<ActionResult<VirtualNetworkConfiguration>> HandleGetRequest(Func<ISingleResultSpecification<Project>> specificationFunc, CancellationToken cancellationToken)
        {
            var projectSpec = specificationFunc();

            var project= await _stateStore.Read<Project>().GetBySpecAsync(projectSpec, cancellationToken);

            if (project == null)
                return new NotFoundResult();
            
            var networks = await _stateStore.For<VirtualNetwork>().ListAsync(new VirtualNetworkSpecs.GetForProjectConfig(project.Id), cancellationToken);

            var projectConfig = networks.ToNetworksConfig(project.Name);

            var configString = ConfigModelJsonSerializer.Serialize(projectConfig);

            var result = new VirtualNetworkConfiguration()
            {
                Configuration = JsonSerializer.Deserialize<JsonElement>(configString)
            };

            return result;
        }
    }
}
