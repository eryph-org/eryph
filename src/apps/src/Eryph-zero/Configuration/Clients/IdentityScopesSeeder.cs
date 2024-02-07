using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eryph.Configuration;
using Eryph.Modules.Identity;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Eryph.Runtime.Zero.Configuration.Clients;

[UsedImplicitly]
internal class IdentityScopesSeeder : IConfigSeeder<IdentityModule>
{
    private readonly ILogger<IdentityScopesSeeder> _logger;
    private readonly IOpenIddictScopeManager _scopeManager;

    public IdentityScopesSeeder(ILogger<IdentityScopesSeeder> logger, IOpenIddictScopeManager scopeManager)
    {
        _logger = logger;
        _scopeManager = scopeManager;
    }

    public static Dictionary<string, string[]> Scopes = new()
    {
        {
            "compute:read", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:write", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:catlets:read", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:catlets:write", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:catlets:control", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:projects:read", new[]
            {
                "compute_api"
            }
        },
        {
            "compute:projects:write", new[]
            {
                "compute_api"
            }
        },
        {
            "identity:clients:read", new[]
            {
                "identity_api"
            }
        },
        {
            "identity:clients:write", new[]
            {
                "identity_api"
            }
        },
        {
            "identity:read", new[]
            {
                "identity_api"
            }
        },
        {
            "identity:write", new[]
            {
                "identity_api"
            }
        }
    };

    public async Task Execute(CancellationToken stoppingToken)
    {
        foreach (var scope in Scopes)
        {
            if (await _scopeManager.FindByNameAsync(scope.Key, stoppingToken) is not null) continue;

            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = scope.Key,
            };
                    
            descriptor.Resources.UnionWith(scope.Value);

            try
            {
                await _scopeManager.CreateAsync(descriptor, stoppingToken);
                _logger.LogDebug("loaded scope {scopeName}", scope.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "failed to load scope {scopeName}", scope.Key);
            }
        }



    }
}