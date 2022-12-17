using System;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.ComputeApi.Handlers;
using Eryph.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using SimpleInjector;

namespace Eryph.Modules.ComputeApi
{
    [UsedImplicitly]
    public class ComputeApiModule : ApiModule<ComputeApiModule>
    {
        private readonly IEndpointResolver _endpointResolver;

        public ComputeApiModule(IEndpointResolver endpointResolver)
        {
            _endpointResolver = endpointResolver;
        }

        public override string Path => _endpointResolver.GetEndpoint("compute").ToString();

        public override string ApiName => "Compute Api";
        public override string AudienceName => "compute_api";


        public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            container.Register<IGetRequestHandler<StateDb.Model.VirtualCatlet, VirtualCatletConfiguration>, 
                GetVirtualCatletConfigurationHandler>();

            base.ConfigureContainer(serviceProvider, container);
        }
    }

}