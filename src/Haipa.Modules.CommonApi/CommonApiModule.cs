using System;
using AutoMapper;
using AutoMapper.Configuration;
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
