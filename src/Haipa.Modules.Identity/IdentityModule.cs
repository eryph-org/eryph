using System;
using System.Collections.Generic;
using AuthorizationServer.Services;
using Haipa.IdentityDb;
using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using ApplicationUser = Haipa.IdentityDb.ApplicationUser;

namespace Haipa.Modules.Identity
{
    public class IdentityModule : WebModuleBase
    {
        public override string Name => "Haipa.Modules.Identity";
        public override string Path => "identity";


        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {

            services.AddMvc()
                .ConfigureAsRazorModule();


            services.AddDbContext<IdentityDbContext>(options =>
            {
                serviceProvider.GetService<IDbContextConfigurer<IdentityDbContext>>().Configure(options);

            });

            // Register the Identity services.

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddDefaultTokenProviders();

            services.AddIdentityServer()
                .AddDeveloperSigningCredential()
                .AddTestUsers(new List<TestUser> { new TestUser{ Username = "frank", Password = "hello", SubjectId = "frank"} })
                .AddAppAuthRedirectUriValidator()
                .AddInMemoryClients(new List<Client>
                {
                    new Client()
                    {
                        ClientId = "console",
                        RedirectUris = new List<string>(new [] {"http://127.0.0.1"}),
                        AllowedGrantTypes = GrantTypes.HybridAndClientCredentials,
                        RequirePkce = true,
                        AllowOfflineAccess = true,
                        //ClientSecrets = new List<Secret>(new[]
                        //{
                        //    new Secret
                        //    {
                        //        Value = "peng".Sha256()
                        //    },
                        //}),
                        RequireClientSecret = false,
                        RequireConsent = false,
                        
                        AllowedScopes = new [] { "openid", IdentityServerConstants.StandardScopes.OfflineAccess }

                    }
                })
                .AddInMemoryApiResources(new List<ApiResource>
                {
                    new ApiResource()
                })
                .AddInMemoryCaching()
                .AddInMemoryIdentityResources(
                    new[]
                    {
                        new IdentityResources.OpenId()

                });


            //services.AddTenant();

            //services.AddApiVersioning(options =>
            //{
            //    options.ReportApiVersions = true;
            //    options.AssumeDefaultVersionWhenUnspecified = false;
            //});


            //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            //JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //    .AddJwtBearer(options =>
            //    {
            //        options.Authority = "https://localhost:62189/identity";
            //        options.Audience = "identity_api";
            //        options.RequireHttpsMetadata = false;
            //    });

            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("identity:apps:read:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:read:all", "http://localhost:62189/identity")));
            //});
            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("identity:apps:write:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:write:all", "http://localhost:62189/identity")));
            //});

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
            services.AddTransient<IEmailSender, AuthMessageSender>();
            services.AddTransient<ISmsSender, AuthMessageSender>();
        }

        protected override void Configure(IApplicationBuilder app)
        {
            //app.UseTenant();

            app.UseStaticFiles();

            app.UseIdentityServer();
            

            app.UseAuthentication();

            app.UseMvcWithDefaultRoute();


        }

    }
}
