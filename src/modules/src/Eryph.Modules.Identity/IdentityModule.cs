using System;
using System.Collections.Generic;
using System.Linq;
using Asp.Versioning;
using Dbosoft.Hosuto.Modules;
using Dbosoft.Rebus;
using Dbosoft.Rebus.Configuration;
using Eryph.Core;
using Eryph.Messages.Components;
using Eryph.ModuleCore.Authorization;
using Eryph.ModuleCore.Components;
using Eryph.IdentityDb;
using Eryph.IdentityDb.Entities;
using Eryph.ModuleCore;
using Eryph.ModuleCore.Startup;
using Eryph.Rebus;
using Microsoft.Extensions.Logging;
using Rebus.Config;
using Rebus.Handlers;
using Rebus.Subscriptions;
using System.IO.Abstractions;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.Identity.ChangeTracking;
using Eryph.Modules.Identity.Events;
using Eryph.Modules.Identity.Events.Validations;
using Eryph.Modules.Identity.Services;
using Eryph.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.EntityFrameworkCore.Models;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using static OpenIddict.Server.OpenIddictServerHandlers.Exchange;

namespace Eryph.Modules.Identity;

[ApiVersion("1.0")]
public class IdentityModule(IEndpointResolver endpointResolver, IConfiguration configuration) : WebModule
{
    private readonly IdentityChangeTrackingConfig _changeTrackingConfig = BindChangeTracking(configuration);

    private static IdentityChangeTrackingConfig BindChangeTracking(IConfiguration configuration)
    {
        var config = new IdentityChangeTrackingConfig();
        configuration.GetSection("IdentityChangeTracking").Bind(config);
        return config;
    }

    public override string Path => endpointResolver.GetEndpoint("identity").ToString();

#pragma warning disable S2325
    // ReSharper disable once UnusedMember.Global
    public void AddSimpleInjector(SimpleInjectorAddOptions options)
#pragma warning restore S2325
    {
        options.AddAspNetCore().AddControllerActivation();

        // Register as a deployment component and advertise the identity endpoint. Wired in the
        // module so every packaging (eryph-zero in-memory, standalone RabbitMQ) registers
        // identically; the host only supplies the transport.
        options.AddComponentRegistration(
            ComponentType.Identity,
            $"{QueueNames.IdentityServices}.{ComponentIdentity.GetLocalHostId()}",
            new Dictionary<string, string>
            {
                ["identity"] = endpointResolver.GetEndpoint("identity").ToString(),
            });

        // Migrate the identity database before anything reads or seeds it (no-op on the in-memory
        // provider used by tests).
        options.AddStartupHandler<MigrateIdentityDbHandler>();

        // Mirror the state store's change-tracking/export pipeline for the identity database. Register
        // change tracking BEFORE seeding so the export queues are enabled before the seeders save (and
        // re-export) the rebuilt rows. Off by default in server mode; on for eryph-zero and for taking a
        // backup / live DB migration.
        if (_changeTrackingConfig.TrackChanges)
            options.AddIdentityChangeTracking();

        options.AddIdentitySeeding();

        options.AddLogging();
    }

#pragma warning disable S2325
    // ReSharper disable once UnusedMember.Global
    public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services,
#pragma warning restore S2325
        IHostEnvironment env)
    {
        var authority = endpointResolver.GetEndpoint("identity").ToString();
        var signingCertManager = serviceProvider.GetRequiredService<ITokenCertificateManager>();

        services.AddDbContext<IdentityDbContext>(options =>
        {
            serviceProvider.GetRequiredService<IDbContextConfigurer<IdentityDbContext>>().Configure(options);
            // OpenIddict's tables are contributed through this options extension; the design-time
            // migration factory applies the same call so generated migrations include them.
            IdentityDbModel.ApplyOpenIddict(options);
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
                // An HTTP authority (split-runtime dev, behind TLS termination) cannot serve HTTPS
                // metadata; in production the authority is HTTPS and metadata stays HTTPS-only.
                if (authority.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    options.RequireHttpsMetadata = false;
            });


        services.AddAuthorization(options =>
        {
            ConfigureIdentityScopes(options, authority);
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

                // Client assertions (private_key_jwt) are validated natively by OpenIddict since v5:
                // the client's public key is registered as a JsonWebKeySet on the application
                // (see ClientService) and the server validates incoming assertions automatically.
                // The 4.x manual workaround for https://github.com/openiddict/openiddict-core/issues/1251
                // is therefore no longer required.

                // Advertise to clients that this server expects the issuer as the client-assertion
                // audience (OpenIddict 7.0+ behaviour). Older clients that don't read this flag keep
                // using the legacy token-endpoint audience against older servers.
                options.AddEventHandler(AdvertiseClientAssertionAudience.Descriptor);

                // replace built-in scope permission validation with hierarchy-aware validation
                options.RemoveEventHandler(ValidateScopePermissions.Descriptor);
                options.AddEventHandler(ValidateScopePermissionsHandler.Descriptor);

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
        container.Register(sp.GetRequiredService<ICertificateStoreService>);
        container.Register(sp.GetRequiredService<IEndpointResolver>);
        container.Register(typeof(IIdentityDbRepository<>), typeof(IdentityDbRepository<>), Lifestyle.Scoped);
        container.Register<IClientService, ClientService>(Lifestyle.Scoped);

        // Change-tracking/export config + file system, used by the export handlers and the seeders.
        container.RegisterInstance(_changeTrackingConfig);
        container.RegisterSingleton<IFileSystem, FileSystem>();

        container.Register<IUserInfoProvider, UserInfoProvider>(Lifestyle.Scoped);

        // Component PKI + enrollment. The CA issues component mTLS and server-TLS certificates from
        // a single root; the enrollment service issues to authorized requests. The policy is the
        // swappable seam (default = operator-minted one-time enrollment token; if no/invalid token is
        // presented, enrollment is denied). The CA is stateless over the certificate store, so it is
        // registered transient to match the (transient) cross-wired certificate services. Loggers are
        // built from the cross-wired ILoggerFactory.
        container.Register<IComponentCertificateAuthority, ComponentCertificateAuthority>();
        // The redeemer is NOT stateless — it records redeemed token ids through the IdentityDb
        // repository (scoped). It is transient so each resolution (within the enrollment request scope,
        // via the policy/service factories below) gets a fresh scoped repository, never a captured one.
        container.Register<IEnrollmentTokenRedeemer, EnrollmentTokenRedeemer>();
        container.Register<IComponentEnrollmentPolicy>(() =>
            new TokenEnrollmentPolicy(
                container.GetInstance<IEnrollmentTokenRedeemer>(),
                container.GetInstance<ILoggerFactory>().CreateLogger<TokenEnrollmentPolicy>()));
        container.Register<IComponentEnrollmentService>(() =>
            new ComponentEnrollmentService(
                container.GetInstance<IComponentCertificateAuthority>(),
                container.GetInstance<IComponentEnrollmentPolicy>(),
                container.GetInstance<ILoggerFactory>().CreateLogger<ComponentEnrollmentService>()));

        // The identity component runs a bidirectional bus endpoint on its own inbound queue
        // (from the registered ComponentIdentity) so it can register/heartbeat and receive
        // config. The host supplies the transport (in-memory for eryph-zero, RabbitMQ for split).
        // This scan registers any IHandleMessages<> in the identity module's own assembly; the
        // config-distribution handlers (ConfigSnapshotCommand/PushConfigCommand) are registered
        // separately by AddComponentRegistration against their closed-generic handler services.
        container.Collection.Register(typeof(IHandleMessages<>), typeof(IdentityModule).Assembly);
        container.ConfigureRebus(configurer => configurer
            .Serialization(s => s.UseEryphSettings())
            .Transport(t =>
                container.GetInstance<IRebusTransportConfigurer>()
                    .Configure(t, container.GetInstance<ComponentIdentity>().InboundQueue))
            .Options(x => x.SetNumberOfWorkers(2))
            .Subscriptions(s =>
                container.GetService<IRebusConfigurer<ISubscriptionStorage>>()?.Configure(s))
            .Logging(x => x.MicrosoftExtensionsLogging(container.GetInstance<ILoggerFactory>()))
            .Start());
    }

    public static void ConfigureIdentityScopes(AuthorizationOptions options, string authority)
    {
        // Create policies for each scope using hierarchy-aware scope resolution
        foreach (var scope in ScopeDefinitions.IdentityApiScopes)
        {
            CreateIdentityScopePolicy(options, authority, scope);
        }
    }

    private static void CreateIdentityScopePolicy(AuthorizationOptions options, string authority, string requiredScope)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUserOrSwaggerEndpoint()
            .Build();

        // Get all scopes that can satisfy this requirement (including higher-level scopes)
        var allowedScopes = ScopeHierarchy.GetGrantingScopes(requiredScope);

        options.AddPolicy(requiredScope,
            policy => policy.Requirements.Add(new HasScopeRequirement(
                authority,
                allowedScopes)));
    }
}
