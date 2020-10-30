using System;
using AutoMapper;
using AutoMapper.Configuration;
using Haipa.Modules.AspNetCore;
using Haipa.Modules.CommonApi.Models.V1;
using JetBrains.Annotations;
using SimpleInjector;

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
