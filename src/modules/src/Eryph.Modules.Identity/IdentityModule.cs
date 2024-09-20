using System;
using Asp.Versioning;
using Dbosoft.Hosuto.Modules;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.Identity.Events.Validations;
using Eryph.Modules.Identity.Models.V1;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using Eryph.StateDb;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.EntityFrameworkCore.Models;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using static Eryph.Modules.Identity.Events.ClientAssertionFilters;
using static OpenIddict.Server.OpenIddictServerHandlers.Exchange;

namespace Eryph.Modules.Identity
{
    /// <summary>
    ///     Defines the <see cref="IdentityModule" />
    /// </summary>
    [ApiVersion("1.0")]
    public class IdentityModule : WebModule
    {
        private readonly IEndpointResolver _endpointResolver;

        public IdentityModule(IEndpointResolver endpointResolver)
        {
            _endpointResolver = endpointResolver;
        }

        public override string Path => _endpointResolver.GetEndpoint("identity").ToString();

        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddAspNetCore()
                .AddControllerActivation();
        }

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
            IHostEnvironment env)
        {
            var endpointResolver = serviceProvider.GetRequiredService<IEndpointResolver>();
            var authority = endpointResolver.GetEndpoint("identity").ToString();
            var signingCertManager = serviceProvider.GetRequiredService<ISigningCertificateManager>();

            services.AddDbContext<IdentityDbContext>(options =>
            {
                serviceProvider.GetRequiredService<IdentityDb.IDbContextConfigurer<IdentityDbContext>>().Configure(options);
                options.UseOpenIddict<ApplicationEntity, AuthorizationEntity, OpenIddictEntityFrameworkCoreScope, TokenEntity, string>();
            });

            var encryptionCertificate = signingCertManager.GetEncryptionCertificate();
            var signingCertificate = signingCertManager.GetSigningCertificate();

            services.AddMvc()
                .AddApiProvider<IdentityModule>(op => op.ApiName = "Eryph Identity Api");

            //services.AddSingleton<IModelConfiguration, ODataModelConfiguration>();


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = authority;
                    options.Audience = "identity_api";
                });


            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:clients:read",
                    policy => policy.Requirements.Add(new HasScopeRequirement(
                        authority,
                        "identity:clients:read", "identity:clients:write", "identity:read", "identity:write")));
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:clients:write",
                    policy => policy.Requirements.Add(new HasScopeRequirement(
                        authority,
                        "identity:clients:write", "identity:write")));
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
            container.Register<ICertificateGenerator, CertificateGenerator>();
            container.Register(sp.GetRequiredService<ICertificateKeyService>);
            container.Register(sp.GetRequiredService<IEndpointResolver>);
            container.Register(typeof(IIdentityDbRepository<>), typeof(IdentityDbRepository<>), Lifestyle.Scoped);
            container.Register<IClientService, ClientService>(Lifestyle.Scoped);

            container.Register<IUserInfoProvider, UserInfoProvider>(Lifestyle.Scoped);

        }

    }
}