using System;
using Ardalis.Specification;
using Haipa.ModuleCore;
using Haipa.Modules.AspNetCore;
using Haipa.Modules.AspNetCore.ApiProvider;
using Haipa.Modules.ComputeApi.Model.V1;
using JetBrains.Annotations;
using SimpleInjector;

namespace Haipa.Modules.ComputeApi
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

    }

}