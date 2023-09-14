using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace Eryph.Modules.Identity.Events.Validations;

public class ValidateClientSecretEvents
{
    public sealed class BuildInValidateClientSecret
    {
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
                .AddFilter<OpenIddictServerHandlerFilters.RequireClientIdParameter>()
                .AddFilter<OpenIddictServerHandlerFilters.RequireDegradedModeDisabled>()
                .AddFilter<ClientAssertionFilters.RequireNoClientAssertion>()
                .UseScopedHandler<OpenIddictServerHandlers.Exchange.ValidateClientSecret>()
                .SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientType.Descriptor.Order + 1_000)
                .SetType(OpenIddictServerHandlerType.BuiltIn)
                .Build();
    }

    public sealed class ValidateClientAssertion : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
    {
        private readonly IOpenIddictApplicationManager _applicationManager;

        public ValidateClientAssertion() => throw new InvalidOperationException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0016));

        public ValidateClientAssertion(IOpenIddictApplicationManager applicationManager)
            => _applicationManager = applicationManager ?? throw new ArgumentNullException(nameof(applicationManager));

        /// <summary>
        /// Gets the default descriptor definition assigned to this handler.
        /// </summary>
        public static OpenIddictServerHandlerDescriptor Descriptor { get; }
            = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenRequestContext>()
                .AddFilter<OpenIddictServerHandlerFilters.RequireClientIdParameter>()
                .AddFilter<OpenIddictServerHandlerFilters.RequireDegradedModeDisabled>()
                .AddFilter<ClientAssertionFilters.RequireClientAssertion>()
                .UseScopedHandler<ValidateClientAssertion>()
                .SetOrder(OpenIddictServerHandlers.Authentication.ValidateClientType.Descriptor.Order + 1_000)
                .SetType(OpenIddictServerHandlerType.Custom)
                .Build();

        /// <inheritdoc/>
        public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(!string.IsNullOrEmpty(context.ClientId),
                OpenIddictResources.FormatID4000(OpenIddictConstants.Parameters.ClientId));

            Debug.Assert(!string.IsNullOrEmpty(context.Request.ClientAssertion), 
                OpenIddictResources.FormatID4000(OpenIddictConstants.Parameters.ClientAssertion));

            if (context.Request.ClientAssertionType != "urn:ietf:params:oauth:client-assertion-type:jwt-bearer")
            {
                context.Reject(
                    error: OpenIddictConstants.Errors.InvalidRequest,
                    description: "Client Assertion Type is not valid",
                    uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2055));
            }


            var application = await _applicationManager.FindByClientIdAsync(context.ClientId) as ClientApplicationEntity ??
                              throw new InvalidOperationException(OpenIddictResources.GetResourceString(OpenIddictResources.ID0032));

            // If the application is a public client, don't validate the client secret.
            if (await _applicationManager.HasClientTypeAsync(application, OpenIddictConstants.ClientTypes.Public))
            {
                return;
            }

            var certificate = new X509Certificate2(Convert.FromBase64String(application.Certificate));
            var securityKey = new X509SecurityKey(certificate);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = application.ClientId,
                ValidateAudience = true,
                ValidAudience = context.BaseUri + "/connect/token",
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = new[] { securityKey },
                ValidateLifetime = true
            };

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                tokenHandler.ValidateToken(context.Request.ClientAssertion, validationParameters, out var validatedToken);
                var jwt = (JwtSecurityToken)validatedToken;

            }
            catch (SecurityTokenValidationException ex)
            {
                // Log the reason why the token is not valid
                context.Logger.LogInformation(ex,
                    OpenIddictResources.GetResourceString(OpenIddictResources.ID6085), context.ClientId);

                context.Reject(
                    error: OpenIddictConstants.Errors.InvalidClient,
                    description: OpenIddictResources.GetResourceString(OpenIddictResources.ID2055),
                    uri: OpenIddictResources.FormatID8000(OpenIddictResources.ID2055));
            }

        }
    }
}