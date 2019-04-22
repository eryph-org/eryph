using System;
using System.Threading;
using System.Threading.Tasks;
using Haipa.IdentityDb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;

namespace Haipa.Modules.Identity
{
    public class IdentityInitializer : IModuleHandler
    {
        private readonly IdentityDbContext _dbContext;
        private readonly OpenIddictApplicationManager<OpenIddictApplication> _manager;

        public IdentityInitializer(IdentityDbContext dbContext, OpenIddictApplicationManager<OpenIddictApplication> manager)
        {
            _dbContext = dbContext;
            _manager = manager;
        }

        public async Task Execute(CancellationToken stoppingToken)
        {
            if(!_dbContext.Database.IsInMemory())
                await _dbContext.Database.MigrateAsync().ConfigureAwait(false);

            if (await _manager.FindByClientIdAsync("console") == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "console",
                    ClientSecret = "388D45FA-B36B-4988-BA59-B187D329C207",
                    DisplayName = "My client application",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                    }
                };

                await _manager.CreateAsync(descriptor);
            }


            if (await _manager.FindByClientIdAsync("system-client") == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "system-client",
                    ClientSecret = "388D45FA-B36B-4988-BA59-B187D329C207",
                    DisplayName = $"Local System client for host ${Environment.MachineName}",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials
                    }
                };

                await _manager.CreateAsync(descriptor);
            }

        }
    }
}