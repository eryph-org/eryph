using System;
using Dbosoft.Hosuto.Modules;
using Haipa.Modules.ApiProvider;
using Haipa.Modules.ApiProvider.Services;
using Haipa.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.CommonApi
{
    [UsedImplicitly]
    public class CommonApiModule : ApiModule<CommonApiModule>
    {
        public override string Name => "Haipa.Modules.CommonApi";
        public override string Path => "common";

        public override string ApiName => "Common Api";
        public override string AudienceName => "common_api";

    }


}
