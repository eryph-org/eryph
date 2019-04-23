using System;
using System.Collections.Concurrent;
using Haipa.IdentityDb;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Haipa.Modules.Identity
{
    public class OpenIddictServerOptionsProvider : IOptionsMonitor<OpenIddictServerOptions>
    {
        private readonly ConcurrentDictionary<(string name, string tenant), Lazy<OpenIddictServerOptions>> _cache;
        private readonly IOptionsFactory<OpenIddictServerOptions> _optionsFactory;
        private readonly ITenantProvider _tenantProvider;

        public OpenIddictServerOptionsProvider(
            IOptionsFactory<OpenIddictServerOptions> optionsFactory,
            ITenantProvider tenantProvider)
        {
            _cache = new ConcurrentDictionary<(string, string), Lazy<OpenIddictServerOptions>>();
            _optionsFactory = optionsFactory;
            _tenantProvider = tenantProvider;
        }

        public OpenIddictServerOptions CurrentValue => Get(Options.DefaultName);

        public OpenIddictServerOptions Get(string name)
        {
            var tenant = _tenantProvider.GetCurrentTenant();

            Lazy<OpenIddictServerOptions> Create() => new Lazy<OpenIddictServerOptions>(() => _optionsFactory.Create(name));
            return _cache.GetOrAdd((name, tenant), _ => Create()).Value;
        }

        public IDisposable OnChange(Action<OpenIddictServerOptions, string> listener) => null;
    }
}