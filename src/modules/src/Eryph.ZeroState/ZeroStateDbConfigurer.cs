using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Eryph.StateDb;
using Microsoft.EntityFrameworkCore;
using SimpleInjector;

namespace Eryph.ZeroState
{
    internal class ZeroStateDbConfigurer : IDbContextConfigurer<StateStoreContext>
    {
        private readonly IDbContextConfigurer<StateStoreContext> _decoratedConfigurer;
        private readonly ZeroStateVirtualNetworkInterceptor _interceptor;

        public ZeroStateDbConfigurer(
            IDbContextConfigurer<StateStoreContext> decoratedConfigurer,
            ZeroStateVirtualNetworkInterceptor interceptor)
        {
            _decoratedConfigurer = decoratedConfigurer;
            _interceptor = interceptor;
        }

        public void Configure(DbContextOptionsBuilder options)
        {
            _decoratedConfigurer.Configure(options);
            options.AddInterceptors(_interceptor);
        }
    }
}
