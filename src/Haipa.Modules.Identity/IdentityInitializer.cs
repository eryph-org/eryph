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

            if (await _manager.FindByClientIdAsync("console") != null)
            {
                var app = await _manager.FindByClientIdAsync("console");
                await _manager.DeleteAsync(app);
            }

            if (await _manager.FindByClientIdAsync("console") == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "console",
                    //ClientSecret = "388D45FA-B36B-4988-BA59-B187D329C207",
                    DisplayName = "My client application",
                    RedirectUris = { new Uri("http://127.0.0.1:7890/")},
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Authorization,
                        OpenIddictConstants.Permissions.Endpoints.Logout,
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddictConstants.Permissions.Scopes.Email,
                        OpenIddictConstants.Permissions.Scopes.Profile,
                        OpenIddictConstants.Permissions.Scopes.Roles,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity:apps:read:all",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity:apps:write:all"
                    }
                };

                await _manager.CreateAsync(descriptor);
            }

            if (await _manager.FindByClientIdAsync("C14E96E2-28DD-4AE4-B5CC-D78ED62E7AF9") == null)
            {
                var descriptor = new OpenIddictApplicationDescriptor
                {
                    ClientId = "C14E96E2-28DD-4AE4-B5CC-D78ED62E7AF9",
                    ClientSecret = "388D45FA-B36B-4988-BA59-B187D329C207",
                    DisplayName = "My client application",
                    Permissions =
                    {
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity:apps:read:all",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "identity:apps:write:all"
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
                    },
                    
                   
                };
                await _manager.CreateAsync(descriptor);
            }

        }
    }
}