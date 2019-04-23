using Haipa.IdentityDb;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using OpenIddict.Server;

namespace Haipa.Modules.Identity
{
    public class OpenIddictServerOptionsInitializer : IConfigureNamedOptions<OpenIddictServerOptions>
    {
        private readonly IDataProtectionProvider _dataProtectionProvider;
        private readonly ITenantProvider _tenantProvider;

        public OpenIddictServerOptionsInitializer(
            IDataProtectionProvider dataProtectionProvider,
            ITenantProvider tenantProvider)
        {
            _dataProtectionProvider = dataProtectionProvider;
            _tenantProvider = tenantProvider;
        }

        public void Configure(string name, OpenIddictServerOptions options) => Configure(options);

        public void Configure(OpenIddictServerOptions options)
        {
            var tenant = _tenantProvider.GetCurrentTenant();

            // Create a tenant-specific data protection provider to ensure authorization codes,
            // access tokens and refresh tokens can't be read/decrypted by the other tenants.
            options.DataProtectionProvider = _dataProtectionProvider.CreateProtector(tenant);

            // Other tenant-specific options can be registered here.
        }
    }
}