using OpenIddict.Server;
using System;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;
using static OpenIddict.Server.OpenIddictServerHandlers.Exchange;
using System.Diagnostics;

namespace Eryph.Modules.Identity.Events.Validations
{
    public static class ValidateClientCredentialsEvents
    {

        public sealed class BuildInValidateClientAssertionParameters : IOpenIddictServerHandler<ValidateTokenRequestContext>
        {
            private readonly ValidateClientCredentialsParameters _buildInHandler = new();

            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
                    .UseSingletonHandler<BuildInValidateClientAssertionParameters>()
                    .AddFilter<ClientAssertionFilters.RequireNoClientAssertion>()
                    .SetOrder(ValidateAuthorizationCodeParameter.Descriptor.Order + 1_000)
                    .SetType(OpenIddictServerHandlerType.Custom)
                    .Build();

            public ValueTask HandleAsync(ValidateTokenRequestContext context)
            {
                return _buildInHandler.HandleAsync(context);
            }
        }

        /// <summary>
        /// Contains the logic responsible for rejecting token requests that don't
        /// specify a valid assertion for the client credentials grant type.
        /// </summary>
        public sealed class ValidateClientAssertionParameters : IOpenIddictServerHandler<ValidateTokenRequestContext>
        {
            /// <summary>
            /// Gets the default descriptor definition assigned to this handler.
            /// </summary>
            public static OpenIddictServerHandlerDescriptor Descriptor { get; }
                = OpenIddictServerHandlerDescriptor.CreateBuilder<ValidateTokenRequestContext>()
                    .UseSingletonHandler<ValidateClientAssertionParameters>()
                    .AddFilter<ClientAssertionFilters.RequireClientAssertion>()
                    .SetOrder(ValidateAuthorizationCodeParameter.Descriptor.Order + 1_001)
                    .SetType(OpenIddictServerHandlerType.Custom)
                    .Build();

            /// <inheritdoc/>
            public ValueTask HandleAsync(ValidateTokenRequestContext context)
            {
                if (context is null)
                {
                    throw new ArgumentNullException(nameof(context));
                }

                Debug.Assert(!string.IsNullOrEmpty(context.ClientId), OpenIddictResources.FormatID4000(Parameters.ClientAssertion));


                if (string.IsNullOrEmpty(context.Request.ClientAssertionType))
                {
                    context.Reject(
                        error: Errors.InvalidRequest,
                        description: OpenIddictResources.FormatID2057(Parameters.ClientId, Parameters.ClientAssertionType),
                        uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2057));
                    return default;

                }

                if (context.Request.ClientAssertionType != "urn:ietf:params:oauth:client-assertion-type:jwt-bearer")
                {
                    context.Reject(
                        error: Errors.InvalidRequest,
                        description: "Client Assertion Type is not valid",
                        uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2057));

                }

                return default;
            }
        }

    }
}
