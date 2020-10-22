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


        public override void ConfigureContainer(IServiceProvider serviceProvider, Container container)
        {
            base.ConfigureContainer(serviceProvider, container);
            container.RegisterSingleton<MapperProvider>();
            container.RegisterSingleton(() => GetMapper(container));
        }

        private AutoMapper.IMapper GetMapper(Container container)
        {
            var mp = container.GetInstance<MapperProvider>();
            return mp.GetMapper();
        }


        public class MapperProvider
        {
            private readonly Container _container;

            public MapperProvider(Container container)
            {
                _container = container;
            }

            public IMapper GetMapper()
            {
                var mce = new MapperConfigurationExpression();
                mce.ConstructServicesUsing(_container.GetInstance);

                mce.AddMaps(typeof(MapperProfile).Assembly);

                var mc = new MapperConfiguration(mce);
                mc.AssertConfigurationIsValid();

                IMapper m = new Mapper(mc, t => _container.GetInstance(t));

                return m;
            }
        }

    }
}
