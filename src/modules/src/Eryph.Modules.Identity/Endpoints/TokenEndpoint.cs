using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Eryph.IdentityDb.Entities;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Eryph.Modules.Identity.Endpoints
{
    public class AuthorizationController : Controller
    {
        private readonly IOpenIddictApplicationManager _applicationManager;
        private readonly IOpenIddictScopeManager _scopeManager;

        public AuthorizationController(IOpenIddictApplicationManager applicationManager, IOpenIddictScopeManager scopeManager)
        {
            _applicationManager = applicationManager;
            _scopeManager = scopeManager;
        }

        [AllowAnonymous]
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
        public async Task<IActionResult> Exchange(CancellationToken cancellationToken)
        {
            var request = HttpContext.GetOpenIddictServerRequest();

            if (request == null || string.IsNullOrWhiteSpace(request.ClientId) )
                return BadRequest();

            if (request.IsClientCredentialsGrantType())
            {
                // Note: the client credentials are automatically validated by OpenIddict:
                // if client_id or client_secret are invalid, this action won't be invoked.

                if (await _applicationManager.FindByClientIdAsync(request.ClientId, cancellationToken) is not ClientApplicationEntity application)
                {
                    throw new InvalidOperationException("The application details cannot be found in the database.");
                }

                // Create the claims-based identity that will be used by OpenIddict to generate tokens.
                var identity = new ClaimsIdentity(
                    authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                    nameType: Claims.Name,
                    roleType: Claims.Role);

                // Add the claims that will be persisted in the tokens (use the client_id as the subject identifier).
                identity.SetClaim(Claims.Subject, await _applicationManager.GetClientIdAsync(application, cancellationToken));
                identity.SetClaim(Claims.Name, await _applicationManager.GetDisplayNameAsync(application, cancellationToken));
                
                if(application.TenantId != default)
                    identity.SetClaim("tid", application.TenantId.ToString());

                if (application.AppRoles is { Length: > 0 })
                    identity.SetClaims(Claims.Role, ImmutableArray.Create(application.AppRoles));

                // Note: In the original OAuth 2.0 specification, the client credentials grant
                // doesn't return an identity token, which is an OpenID Connect concept.
                //
                // As a non-standardized extension, OpenIddict allows returning an id_token
                // to convey information about the client application when the "openid" scope
                // is granted (i.e specified when calling principal.SetScopes()). When the "openid"
                // scope is not explicitly set, no identity token is returned to the client application.

                // Set the list of scopes granted to the client application in access_token.
                var appPermissions = await _applicationManager.GetPermissionsAsync(application, cancellationToken);
                var appScopes = appPermissions.Where(x => x.StartsWith(Permissions.Prefixes.Scope))
                    .Select(x=>x[Permissions.Prefixes.Scope.Length..]);

                var requestScopes = request.GetScopes();

                // add all scopes assigned to application if no scopes requested
                identity.SetScopes(requestScopes.Length() == 0 ? appScopes: requestScopes);
                identity.SetResources(await _scopeManager.ListResourcesAsync(identity.GetScopes(), cancellationToken).ToListAsync());
                identity.SetDestinations(GetDestinations);

                return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
        }

        private static IEnumerable<string> GetDestinations(Claim claim)
        {
            // Note: by default, claims are NOT automatically included in the access and identity tokens.
            // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
            // whether they should be included in access tokens, in identity tokens or in both.

            return claim.Type switch
            {
                "tid" or
                Claims.Name or
                Claims.Subject
                    => new[] { Destinations.AccessToken, Destinations.IdentityToken },

                _ => new[] { Destinations.AccessToken },
            };
        }
    }
}
