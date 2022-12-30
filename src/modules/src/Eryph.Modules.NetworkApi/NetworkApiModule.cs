using System;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider.Handlers;
using Eryph.Modules.NetworkApi.Handlers;
using Eryph.Modules.NetworkApi.Model.V1;
using JetBrains.Annotations;
using SimpleInjector;

namespace Eryph.Modules.NetworkApi
{
    [UsedImplicitly]
    public class NetworkApiModule : ApiModule<NetworkApiModule>
    {
        private readonly IEndpointResolver _endpointResolver;

        public NetworkApiModule(IEndpointResolver endpointResolver)
        {
            _endpointResolver = endpointResolver;
        }

        public override string Path => _endpointResolver.GetEndpoint("network").ToString();

        public override string ApiName => "Network Api";
        public override string AudienceName => "network_api";


        public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {

            base.ConfigureContainer(serviceProvider, container);
        }
    }

}