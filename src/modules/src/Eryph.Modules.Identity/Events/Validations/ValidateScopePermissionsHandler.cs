using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Eryph.ModuleCore.Authorization;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers;

namespace Eryph.Modules.Identity.Events.Validations;

/// <summary>
/// Custom handler that replaces OpenIddict's built-in scope validation with hierarchy-aware validation.
/// This handler validates both scope existence and client permissions using hierarchical scope logic.
/// </summary>
public sealed class ValidateScopePermissionsHandler : IOpenIddictServerHandler<ValidateTokenRequestContext>
{
    private readonly IOpenIddictApplicationManager _applicationManager;
    private readonly IOpenIddictScopeManager _scopeManager;
    private readonly ILogger<ValidateScopePermissionsHandler> _logger;

    public ValidateScopePermissionsHandler() => 
        throw new InvalidOperationException("This handler requires dependency injection.");

    public ValidateScopePermissionsHandler(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<ValidateScopePermissionsHandler> logger)
    {
        _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
        _scopeManager = scopeManager ?? throw new ArgumentNullException(nameof(scopeManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
            .UseScopedHandler<ValidateScopePermissionsHandler>()
            .SetOrder(Exchange.ValidateScopePermissions.Descriptor.Order)
            .SetType(OpenIddictServerHandlerType.BuiltIn)
            .Build();

    public async ValueTask HandleAsync(ValidateTokenRequestContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        // Skip validation if no scopes were requested
        var requestedScopes = context.Request.GetScopes();
        if (!requestedScopes.Any())
        {
            return;
        }

        // First, validate that all requested scopes exist in the scope store
        foreach (var scope in requestedScopes)
        {
            var scopeEntity = await _scopeManager.FindByNameAsync(scope, context.CancellationToken);
            if (scopeEntity == null)
            {
                _logger.LogWarning("The scope '{Scope}' is not registered in the scope store", scope);
                context.Reject(
                    error: OpenIddictConstants.Errors.InvalidScope,
                    description: "The specified scope is not supported.");
                return;
            }
        }

        // Get the client application
        var application = await _applicationManager.FindByClientIdAsync(context.ClientId!, context.CancellationToken);
        if (application == null)
        {
            context.Logger.LogError("The client application associated with '{ClientId}' cannot be found.", context.ClientId);
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidClient,
                description: "The client application cannot be found.");
            return;
        }

        // Get the permissions associated with the application
        var applicationPermissions = await _applicationManager.GetPermissionsAsync(application, context.CancellationToken);
        var applicationScopes = applicationPermissions
            .Where(permission => permission.StartsWith(OpenIddictConstants.Permissions.Prefixes.Scope, StringComparison.Ordinal))
            .Select(permission => permission[OpenIddictConstants.Permissions.Prefixes.Scope.Length..])
            .ToImmutableArray();

        _logger.LogDebug("Client '{ClientId}' has the following assigned scopes: {AssignedScopes}", 
            context.ClientId, string.Join(", ", applicationScopes));

        _logger.LogDebug("Client '{ClientId}' is requesting the following scopes: {RequestedScopes}", 
            context.ClientId, string.Join(", ", requestedScopes));

        // Validate each requested scope using hierarchical scope logic
        var invalidScopes = requestedScopes
            .Where(scope => !IsRequestedScopeAllowed(scope, applicationScopes))
            .ToArray();

        if (invalidScopes.Any())
        {
            _logger.LogWarning("Client '{ClientId}' requested invalid scopes: {InvalidScopes}. " +
                "Available scopes: {AvailableScopes}", 
                context.ClientId, 
                string.Join(", ", invalidScopes),
                string.Join(", ", ScopeHierarchy.GetAvailableScopes(applicationScopes)));

            context.Reject(
                error: OpenIddictConstants.Errors.InvalidScope,
                description: "The specified scope is not supported.");
            return;
        }

        _logger.LogDebug("All requested scopes are valid for client '{ClientId}'", context.ClientId);
    }

    private static bool IsRequestedScopeAllowed(string requestedScope, ImmutableArray<string> applicationScopes)
    {
        // Handle built-in OpenIddict scopes that are typically auto-approved
        if (requestedScope == OpenIddictConstants.Scopes.OpenId || 
            requestedScope == OpenIddictConstants.Scopes.OfflineAccess)
        {
            return true;
        }

        // Use hierarchical scope validation for application-specific scopes
        return ScopeHierarchy.IsScopeAllowed(requestedScope, applicationScopes);
    }
}