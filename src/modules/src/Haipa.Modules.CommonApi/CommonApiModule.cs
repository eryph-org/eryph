using Haipa.ModuleCore;
using Haipa.Modules.AspNetCore;
using JetBrains.Annotations;

namespace Haipa.Modules.CommonApi
{
    [UsedImplicitly]
    public class CommonApiModule : ApiModule<CommonApiModule>
    {
        private readonly IEndpointResolver _endpointResolver;

        public CommonApiModule(IEndpointResolver endpointResolver)
        {
            _endpointResolver = endpointResolver;
        }

        public override string Path => _endpointResolver.GetEndpoint("common").ToString();

        public override string ApiName => "Common Api";
        public override string AudienceName => "common_api";
    }
}