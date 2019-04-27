﻿using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Extensions;
using AspNet.Security.OpenIdConnect.Primitives;
using AuthorizationServer.Helpers;
using AuthorizationServer.ViewModels.Authorization;
using AuthorizationServer.ViewModels.Shared;
using Haipa.IdentityDb;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Mvc.Internal;
using OpenIddict.Server;

namespace Haipa.Modules.Identity.Controller
{

    public class AuthorizationController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly OpenIddictApplicationManager<OpenIddictApplication> _applicationManager;
        private readonly IOptions<IdentityOptions> _identityOptions;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthorizationController(
            OpenIddictApplicationManager<OpenIddictApplication> applicationManager,
            IOptions<IdentityOptions> identityOptions,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager)
        {
            _applicationManager = applicationManager;
            _identityOptions = identityOptions;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [Authorize, HttpGet("~/connect/authorize")]
        public async Task<IActionResult> Authorize([ModelBinder(typeof(OpenIddictMvcBinder))] OpenIdConnectRequest request)
        {
            // Retrieve the application details from the database.
            var application = await _applicationManager.FindByClientIdAsync(request.ClientId, HttpContext.RequestAborted);
            if (application == null)
            {
                return View("Error", new ErrorViewModel
                {
                    Error = OpenIdConnectConstants.Errors.InvalidClient,
                    ErrorDescription = "Details concerning the calling client application cannot be found in the database"
                });
            }

            // Flow the request_id to allow OpenIddict to restore
            // the original authorization request from the cache.
            return View(new AuthorizeViewModel
            {
                ApplicationName = application.DisplayName,
                RequestId = request.RequestId,
                Scope = request.Scope
            });
        }

        [Authorize, FormValueRequired("submit.Accept")]
        [HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept([ModelBinder(typeof(OpenIddictMvcBinder))] OpenIdConnectRequest request)
        {
            // Retrieve the profile of the logged in user.
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return View("Error", new ErrorViewModel
                {
                    Error = OpenIdConnectConstants.Errors.ServerError,
                    ErrorDescription = "An internal error has occurred"
                });
            }

            // Create a new authentication ticket.
            var ticket = await CreateTicketAsync(request, user);

            // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
            return SignIn(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
        }

        [Authorize, FormValueRequired("submit.Deny")]
        [HttpPost("~/connect/authorize"), ValidateAntiForgeryToken]
        public IActionResult Deny()
        {
            // Notify OpenIddict that the authorization grant has been denied by the resource owner
            // to redirect the user agent to the client application using the appropriate response_mode.
            return Forbid(OpenIddictServerDefaults.AuthenticationScheme);
        }

        [HttpGet("~/connect/logout")]
        public IActionResult Logout([ModelBinder(typeof(OpenIddictMvcBinder))] OpenIdConnectRequest request)
        {
            // Flow the request_id to allow OpenIddict to restore
            // the original logout request from the distributed cache.
            return View(new LogoutViewModel
            {
                RequestId = request.RequestId,
            });
        }

        [HttpPost("~/connect/logout"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Ask ASP.NET Core Identity to delete the local and external cookies created
            // when the user agent is redirected from the external identity provider
            // after a successful authentication flow (e.g Google or Facebook).
            await _signInManager.SignOutAsync();

            // Returning a SignOutResult will ask OpenIddict to redirect the user agent
            // to the post_logout_redirect_uri specified by the client application.
            return SignOut(OpenIddictServerDefaults.AuthenticationScheme);
        }

        [HttpPost("~/connect/token"), Produces("application/json")]
        public async Task<IActionResult> Exchange([ModelBinder(typeof(OpenIddictMvcBinder))] OpenIdConnectRequest request)
        {
            if (request.IsAuthorizationCodeGrantType())
            {
                // Retrieve the claims principal stored in the authorization code.
                var info = await HttpContext.AuthenticateAsync(OpenIddictServerDefaults.AuthenticationScheme);

                // Retrieve the user profile corresponding to the authorization code.
                // Note: if you want to automatically invalidate the authorization code
                // when the user password/roles change, use the following line instead:
                // var user = _signInManager.ValidateSecurityStampAsync(info.Principal);
                var user = await _userManager.GetUserAsync(info.Principal);
                if (user == null)
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The authorization code is no longer valid."
                    });
                }

                // Ensure the user is still allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The user is no longer allowed to sign in."
                    });
                }

                // Create a new authentication ticket, but reuse the properties stored
                // in the authorization code, including the scopes originally granted.
                var ticket = await CreateTicketAsync(request, user, info.Properties);

                return SignIn(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
            }

            if (request.IsClientCredentialsGrantType())
            {
                // Note: the client credentials are automatically validated by OpenIddict:
                // if client_id or client_secret are invalid, this action won't be invoked.

                var application = await _applicationManager.FindByClientIdAsync(request.ClientId, HttpContext.RequestAborted);
                if (application == null)
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidClient,
                        ErrorDescription = "The client application was not found in the database."
                    });
                }

                // Create a new authentication ticket.
                var ticket = CreateTicket(application, request);

                return SignIn(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
            }


            return BadRequest(new OpenIdConnectResponse
            {
                Error = OpenIdConnectConstants.Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }

        private AuthenticationTicket CreateTicket(OpenIddictApplication application, OpenIdConnectRequest request)
        {
            // Create a new ClaimsIdentity containing the claims that
            // will be used to create an id_token, a token or a code.
            var identity = new ClaimsIdentity(
                OpenIddictServerDefaults.AuthenticationScheme,
                OpenIdConnectConstants.Claims.Name,
                OpenIdConnectConstants.Claims.Role);

            // Use the client_id as the subject identifier.
            identity.AddClaim(OpenIdConnectConstants.Claims.Subject, application.ClientId,
                OpenIdConnectConstants.Destinations.AccessToken,
                OpenIdConnectConstants.Destinations.IdentityToken);

            identity.AddClaim(OpenIdConnectConstants.Claims.Name, application.DisplayName,
                OpenIdConnectConstants.Destinations.AccessToken,
                OpenIdConnectConstants.Destinations.IdentityToken);


            // Create a new authentication ticket holding the user identity.
            var ticket = new AuthenticationTicket(
                new ClaimsPrincipal(identity),
                new AuthenticationProperties(),
                OpenIddictServerDefaults.AuthenticationScheme);

            var knownResources = new[] { "resource_server", "identity_api" };
            ticket.SetResources(knownResources);


            ticket.SetScopes(request.GetScopes());

            return ticket;
        }


        private async Task<AuthenticationTicket> CreateTicketAsync(
            OpenIdConnectRequest oidcRequest, ApplicationUser user,
            AuthenticationProperties properties = null)
        {
            // Create a new ClaimsPrincipal containing the claims that
            // will be used to create an id_token, a token or a code.
            var principal = await _signInManager.CreateUserPrincipalAsync(user);

            // Create a new authentication ticket holding the user identity.
            var ticket = new AuthenticationTicket(principal, properties,
                OpenIddictServerDefaults.AuthenticationScheme);

            if (!oidcRequest.IsAuthorizationCodeGrantType())
            {
                // Set the list of scopes granted to the client application.
                // Note: the offline_access scope must be granted
                // to allow OpenIddict to return a refresh token.
                ticket.SetScopes(new[]
                {
                    OpenIdConnectConstants.Scopes.OpenId,
                    OpenIdConnectConstants.Scopes.Email,
                    OpenIdConnectConstants.Scopes.Profile,
                    OpenIdConnectConstants.Scopes.OfflineAccess,
                    OpenIddictConstants.Scopes.Roles
                }.Intersect(oidcRequest.GetScopes()));
            }

            var knownResources = new[] { "resource_server", "identity_api" };
            ticket.SetResources(knownResources);

            // Note: by default, claims are NOT automatically included in the access and identity tokens.
            // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
            // whether they should be included in access tokens, in identity tokens or in both.

            foreach (var claim in ticket.Principal.Claims)
            {
                // Never include the security stamp in the access and identity tokens, as it's a secret value.
                if (claim.Type == _identityOptions.Value.ClaimsIdentity.SecurityStampClaimType)
                {
                    continue;
                }

                var destinations = new List<string>
                {
                    OpenIdConnectConstants.Destinations.AccessToken
                };

                // Only add the iterated claim to the id_token if the corresponding scope was granted to the client application.
                // The other claims will only be added to the access_token, which is encrypted when using the default format.
                if ((claim.Type == OpenIdConnectConstants.Claims.Name && ticket.HasScope(OpenIdConnectConstants.Scopes.Profile)) ||
                    (claim.Type == OpenIdConnectConstants.Claims.Email && ticket.HasScope(OpenIdConnectConstants.Scopes.Email)) ||
                    (claim.Type == OpenIdConnectConstants.Claims.Role && ticket.HasScope(OpenIddictConstants.Claims.Roles)))
                {
                    destinations.Add(OpenIdConnectConstants.Destinations.IdentityToken);
                }

                claim.SetDestinations(destinations);
            }

            return ticket;
        }
    }
}
