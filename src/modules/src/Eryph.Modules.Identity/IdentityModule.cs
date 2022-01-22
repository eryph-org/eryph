using System;
using System.Collections.Generic;
using Dbosoft.Hosuto.Modules;
using Eryph.IdentityDb;
using Eryph.ModuleCore;
using Eryph.Modules.AspNetCore;
using Eryph.Modules.AspNetCore.ApiProvider;
using Eryph.Modules.Identity.Services;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Client = Eryph.Modules.Identity.Models.V1.Client;
using Scope = IdentityServer4.Models.ApiScope;

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
            services.AddMvc()
                .AddApiProvider<IdentityModule>(op => op.ApiName = "Eryph Identity Api");

            //services.AddSingleton<IModelConfiguration, ODataModelConfiguration>();

            services.AddIdentityServer()
                .AddJwtBearerClientAuthentication()
                .AddDeveloperSigningCredential()
                .AddConfigurationStore(options =>
                {
                    options.ConfigureDbContext = builder =>
                        serviceProvider.GetRequiredService<IDbContextConfigurer<ConfigurationDbContext>>()
                            .Configure(builder);
                })
                .AddInMemoryApiResources(new List<ApiResource>
                {
                    new ApiResource("common_api")
                    {
                        Scopes = new List<string>
                        {
                            "compute_api"
                        }
                    },

                    new ApiResource("compute_api")
                    {
                        Scopes = new List<string>
                        {
                            "common_api"
                        }
                    },

                    new ApiResource
                    {
                        Name = "identity_api",
                        Scopes = new List<string>
                        {
                            "identity:clients:write:all",
                            "identity:clients:read:all"
                        }
                    }
                })
                .AddInMemoryApiScopes(new[]
                {
                    new Scope("common_api"),
                    new Scope("compute_api"),

                    new Scope("identity:clients:write:all", "Full access to clients"),
                    new Scope("identity:clients:read:all", "Read only access to clients")
                })
                //.AddInMemoryCaching()
                .AddInMemoryIdentityResources(
                    new[]
                    {
                        new IdentityResources.OpenId()
                    });


            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://localhost:62189/identity";
                    options.Audience = "identity_api";
                });


            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:clients:read:all",
                    policy => policy.Requirements.Add(new HasScopeRequirement("https://localhost:62189/identity",
                        "identity:clients:read:all", "identity:clients:write:all")));
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:clients:write:all",
                    policy => policy.Requirements.Add(new HasScopeRequirement("https://localhost:62189/identity",
                        "identity:clients:write:all")));
            });
        }


        public void Configure(IApplicationBuilder app)
        {
            app.UseIdentityServer();
            app.UseApiProvider(this);
        }

        public void ConfigureContainer(Container container)
        {
            container.Register<IClientRepository, ClientRepository<ConfigurationDbContext>>(Lifestyle.Scoped);
            container.Register<IIdentityServerClientService, IdentityServerClientService>(Lifestyle.Scoped);
            container.Register<IClientService<Client>, ClientService<Client>>(Lifestyle.Scoped);
        }
    }
}