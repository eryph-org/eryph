using System.Collections.Generic;
using Dbosoft.Hosuto.Modules;
using Haipa.IdentityDb;
using Haipa.Modules.ApiProvider;
using Haipa.Modules.Identity.Configuration;
using Haipa.Modules.Identity.Services;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Integration.ServiceCollection;
using Client = Haipa.Modules.Identity.Models.V1.Client;
using Scope = IdentityServer4.Models.Scope;

namespace Haipa.Modules.Identity
{
    using Microsoft.AspNet.OData.Builder;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.DependencyInjection;
    using System;


    /// <summary>
    /// Defines the <see cref="IdentityModule" />
    /// </summary>
    [ApiVersion("1.0")]

    public class IdentityModule : WebModule
    {
        /// <summary>
        /// Gets the Name
        /// </summary>
        public override string Name => "Haipa.Modules.Identity";

        /// <summary>
        /// Gets the Path
        /// </summary>
        public override string Path => "identity";

        public void AddSimpleInjector(SimpleInjectorAddOptions options)
        {
            options.AddAspNetCore()
                .AddControllerActivation();

        }

        public void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services, IHostEnvironment env)
        {

            services.AddMvc()
                .AddApiProvider<IdentityModule>(op => op.ApiName="Haipa Identity Api");

            services.AddSingleton<IModelConfiguration, ODataModelConfiguration>();

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
                    new ApiResource("common_api"),
                    new ApiResource("compute_api"),
                    new ApiResource
                    {
                        Name = "identity_api",
                        Scopes =
                        {
                            new Scope
                            {
                                Name = "identity:clients:write:all",
                                DisplayName = "Full access to clients"
                            },
                            new Scope
                            {
                                Name = "identity:clients:read:all",
                                DisplayName = "Read only access to clients"
                            }
                        }
                    }
                })
                //.AddInMemoryCaching()
                .AddInMemoryIdentityResources(
                    new[]
                    {
                        new IdentityResources.OpenId(),

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