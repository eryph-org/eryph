using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlerFilters;

namespace Eryph.Modules.Identity.Events.Validations;

public class ValidateClientTypeEvents
{
    public sealed class BuildInValidateClientType
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
                .AddFilter<RequireClientIdParameter>()
                .AddFilter<RequireDegradedModeDisabled>()
                .AddFilter<ClientAssertionFilters.RequireNoClientAssertion>()

                .UseScopedHandler<OpenIddictServerHandlers.Exchange.ValidateClientType>()
                .SetOrder(OpenIddictServerHandlers.Exchange.ValidateClientId.Descriptor.Order + 1_000)
                .SetType(OpenIddictServerHandlerType.BuiltIn)
                .Build();
    }

    /// <summary>
    /// Contains the logic responsible for rejecting token requests made by applications
    /// whose client type is not compatible with the requested grant type.
    /// Note: this handler is not used when the degraded mode is enabled.
    /// </summary>
    public sealed class ValidateClientAssertionClientType : IOpenIddictServerHandler<ValidateTokenRequestContext>
    {
        private readonly IOpenIddictApplicationManager _applicationManager;

        public ValidateClientAssertionClientType() =>
            throw new InvalidOperationException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0016));

        public ValidateClientAssertionClientType(IOpenIddictApplicationManager applicationManager)
            => _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));

        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
                .AddFilter<RequireClientIdParameter>()
                .AddFilter<RequireDegradedModeDisabled>()
                .UseScopedHandler<ValidateClientAssertionClientType>()
                .AddFilter<ClientAssertionFilters.RequireClientAssertion>()
                .SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientId.Descriptor.Order + 1000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        /// <inheritdoc/>
        public async ValueTask HandleAsync(ValidateTokenRequestContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(!string.IsNullOrEmpty(context.ClientId), OpenIddictResources.FormatID4000(Parameters.ClientId));
            Debug.Assert(!string.IsNullOrEmpty(context.Request.ClientAssertion),
                OpenIddictResources.FormatID4000(Parameters.ClientAssertion));

            var application = await _applicationManager.FindByClientIdAsync(context.ClientId) as ClientApplicationEntity ??
                              throw new InvalidOperationException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0032));

            if (await _applicationManager.HasClientTypeAsync(application, ClientTypes.Public))
            {
                // Public applications are not allowed to use the client credentials grant.
                if (context.Request.IsClientCredentialsGrantType())
                {
                    context.Logger.LogInformation(OpenIddictResources.GetResourceString(OpenIddictResources.ID6082), context.Request.ClientId);

                    context.Reject(
                        error: Errors.UnauthorizedClient,
                        description: OpenIddictResources.FormatID2043(Parameters.GrantType),
                        uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2043));

                    return;
                }

                // Reject token requests containing a client_secret when the client a public client
                if (!string.IsNullOrEmpty(context.ClientSecret))
                {
                    context.Logger.LogInformation(OpenIddictResources.GetResourceString(OpenIddictResources.ID6083), context.ClientId);

                    context.Reject(
                        error: Errors.InvalidClient,
                        description: OpenIddictResources.FormatID2053(Parameters.ClientSecret),
                        uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2053));

                    return;
                }

                return;
            }

            // application has to contain a certificate
            if (string.IsNullOrEmpty(application.Certificate))
            {
                context.Logger.LogInformation("The token request was rejected because the confidential application '{ClientId}' didn't specify certificate", args: context.ClientId);

                context.Reject(
                    error: Errors.UnauthorizedClient,
                    description: OpenIddictResources.FormatID2043(Parameters.GrantType),
                    uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2043));

                return;
            }


        }
    }

}