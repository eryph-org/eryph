using System;
using System.Collections.Generic;
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
/// This handler validates client permissions using hierarchical scope logic.
/// </summary>
public sealed class ValidateScopePermissionsHandler(
    IOpenIddictApplicationManager applicationManager,
    ILogger<ValidateScopePermissionsHandler> logger)
    : IOpenIddictServerHandler<ValidateTokenRequestContext>
{
    private readonly IOpenIddictApplicationManager _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));
    private readonly ILogger<ValidateScopePermissionsHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
            .Import(Exchange.ValidateScopePermissions.Descriptor)
            .UseScopedHandler<ValidateScopePermissionsHandler>()
            .Build();

    public async ValueTask HandleAsync(ValidateTokenRequestContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        // Get and normalize requested scopes - filter out null/empty/whitespace, trim, and deduplicate
        var requestedScopes = context.Request.GetScopes()
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToHashSet(StringComparer.Ordinal);

        if (requestedScopes.Count == 0)
        {
            return;
        }

        // Get the client application
        var application = await _applicationManager.FindByClientIdAsync(context.ClientId!, context.CancellationToken);
        if (application == null)
        {
            _logger.LogError("The client application associated with '{ClientId}' cannot be found.", context.ClientId);
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

        _logger.LogTrace("Client '{ClientId}' has the following assigned scopes: {AssignedScopes}",
            context.ClientId, string.Join(", ", applicationScopes));

        _logger.LogTrace("Client '{ClientId}' is requesting the following scopes: {RequestedScopes}",
            context.ClientId, string.Join(", ", requestedScopes));

        var expandedApplicationScopes = ScopeHierarchy.ExpandScopes(applicationScopes);

        // Validate each requested scope using hierarchical scope logic
        var invalidScopes = requestedScopes
            .Where(scope => !IsRequestedScopeAllowed(scope, expandedApplicationScopes))
            .ToArray();

        if (invalidScopes.Length > 0)
        {
            _logger.LogDebug("Client '{ClientId}' requested invalid scopes: {InvalidScopes}. " +
                "Available scopes: {AvailableScopes}",
                context.ClientId,
                string.Join(", ", invalidScopes),
                string.Join(", ", expandedApplicationScopes));

            context.Reject(
                error: OpenIddictConstants.Errors.InvalidScope,
                description: "The specified scope is not supported.");
            return;
        }

        _logger.LogDebug("All requested scopes are valid for client '{ClientId}'", context.ClientId);
    }

    private static bool IsRequestedScopeAllowed(string requestedScope, ISet<string> expandedApplicationScopes)
    {
        // Handle built-in OpenIddict scopes
        return requestedScope is OpenIddictConstants.Scopes.OpenId or OpenIddictConstants.Scopes.OfflineAccess
               || expandedApplicationScopes.Contains(requestedScope);
    }
}
