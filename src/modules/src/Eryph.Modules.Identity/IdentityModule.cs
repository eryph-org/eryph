using System;
using System.Linq;
using Asp.Versioning;
using Dbosoft.Hosuto.Modules;
using Eryph.Core;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.Identity.Events.Validations;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.EntityFrameworkCore.Models;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using static Eryph.Modules.Identity.Events.ClientAssertionFilters;
using static OpenIddict.Server.OpenIddictServerHandlers.Exchange;

namespace Eryph.Modules.Identity;

[ApiVersion("1.0")]
public class IdentityModule(IEndpointResolver endpointResolver) : WebModule
{
    public override string Path => endpointResolver.GetEndpoint("identity").ToString();

#pragma warning disable S2325
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
#pragma warning restore S2325
    {
        options.AddAspNetCore().AddControllerActivation();
        options.AddLogging();
    }

#pragma warning disable S2325
    public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
#pragma warning restore S2325
        IHostEnvironment env)
    {
        var authority = endpointResolver.GetEndpoint("identity").ToString();
        var signingCertManager = serviceProvider.GetRequiredService<ITokenCertificateManager>();

        services.AddDbContext<IdentityDbContext>(options =>
        {
            serviceProvider.GetRequiredService<IDbContextConfigurer<IdentityDbContext>>().Configure(options);
            options.UseOpenIddict<ApplicationEntity, AuthorizationEntity, OpenIddictEntityFrameworkCoreScope, TokenEntity, string>();
        });

        var encryptionCertificate = signingCertManager.GetEncryptionCertificate();
        var signingCertificate = signingCertManager.GetSigningCertificate();

        services.AddMvc()
            .AddApiProvider<IdentityModule>(options =>
            {
                options.ApiName = "Eryph Identity Api";
                options.OAuthOptions = new ApiProviderOAuthOptions()
                {
                    TokenEndpoint = new Uri(authority + "/connect/token"),
                    Scopes = EryphConstants.Authorization.AllScopes
                        .Where(s => s.Resources.Contains(EryphConstants.Authorization.Audiences.IdentityApi))
                        .ToList()
                };
            });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = EryphConstants.Authorization.Audiences.IdentityApi;
            });


        services.AddAuthorization(options =>
        {
            options.AddPolicy(EryphConstants.Authorization.Scopes.IdentityClientsRead,
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    EryphConstants.Authorization.Scopes.IdentityClientsRead,
                    EryphConstants.Authorization.Scopes.IdentityClientsWrite,
                    EryphConstants.Authorization.Scopes.IdentityRead,
                    EryphConstants.Authorization.Scopes.IdentityWrite)));
            options.AddPolicy(EryphConstants.Authorization.Scopes.IdentityClientsWrite,
                policy => policy.Requirements.Add(new HasScopeRequirement(
                    authority,
                    EryphConstants.Authorization.Scopes.IdentityClientsWrite,
                    EryphConstants.Authorization.Scopes.IdentityWrite)));
        });

        services.AddOpenIddict()

            // Register the OpenIddict core components.
            .AddCore(options =>
            {
                // Configure OpenIddict to use the Entity Framework Core stores and models.
                // Note: call ReplaceDefaultEntities() to replace the default OpenIddict entities.
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>()
                    .ReplaceDefaultEntities<ApplicationEntity, 
                        AuthorizationEntity,
                        OpenIddictEntityFrameworkCoreScope, TokenEntity, string>();

                //// Enable Quartz.NET integration.
                //options.UseQuartz();
            })

            // Register the OpenIddict server components.
            .AddServer(options =>
            {
                options.DisableAccessTokenEncryption();

                // Enable the token endpoint.
                options.SetTokenEndpointUris("connect/token");
                // Enable the client credentials flow.
                options.AllowClientCredentialsFlow();

                // Register the signing and encryption credentials.
                options.AddSigningCertificate(signingCertificate);
                options.AddEncryptionCertificate(encryptionCertificate);

                // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();

                services.AddScoped<RequireClientAssertion>();
                services.AddScoped<RequireNoClientAssertion>();

                // openiddict currently does not support client_assertion (see https://github.com/openiddict/openiddict-core/issues/1251)
                // we require client assertion only for client applications (see entity ApplicationEntity and ClientApplicationEntity)
                // as workaround we handle client_assertion manually in ValidateClientAssertionClientType and ValidateClientAssertion

                // replace ValidateClientType Descriptor to filter for client_assertion
                options.RemoveEventHandler(ValidateClientType.Descriptor);
                options.AddEventHandler(ValidateClientTypeEvents.BuildInValidateClientType.Descriptor);
                options.AddEventHandler(ValidateClientTypeEvents.ValidateClientAssertionClientType.Descriptor);

                // replace ValidateClientSecret Descriptor to filter for client_assertion
                options.RemoveEventHandler(ValidateClientSecret.Descriptor);
                options.AddEventHandler(ValidateClientSecretEvents.BuildInValidateClientSecret.Descriptor);
                options.AddEventHandler(ValidateClientSecretEvents.ValidateClientAssertion.Descriptor);

                // replace ValidateClientCredentialsParameters Descriptor to filter for client_assertion
                options.RemoveEventHandler(ValidateClientCredentialsParameters.Descriptor);
                options.AddEventHandler(ValidateClientCredentialsEvents.BuildInValidateClientAssertionParameters.Descriptor);
                options.AddEventHandler(ValidateClientCredentialsEvents.ValidateClientAssertionParameters.Descriptor);

            })

            // Register the OpenIddict validation components.
            .AddValidation(options =>
            {
                // Import the configuration from the local OpenIddict server instance.
                options.UseLocalServer();

                // Register the ASP.NET Core host.
                options.UseAspNetCore();

            });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseApiProvider(this);
    }

    [UsedImplicitly]
    public void ConfigureContainer(IServiceProvider sp, Container container)
    {
        container.Register(sp.GetRequiredService<ICertificateKeyService>);
        container.Register(sp.GetRequiredService<ICertificateGenerator>);
        container.Register(sp.GetRequiredService<IEndpointResolver>);
        container.Register(typeof(IIdentityDbRepository<>), typeof(IdentityDbRepository<>), Lifestyle.Scoped);
        container.Register<IClientService, ClientService>(Lifestyle.Scoped);

        container.Register<IUserInfoProvider, UserInfoProvider>(Lifestyle.Scoped);

    }
}
