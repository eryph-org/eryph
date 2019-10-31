using System;
using System.Collections.Generic;
using IdentityServer4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.Identity
{
    public class IdentityModule : WebModuleBase
    {
        public override string Name => "Haipa.Modules.Identity";
        public override string Path => "identity";

        public override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddMvc();

            services.AddIdentityServer()
                .AddJwtBearerClientAuthentication()
                .AddDeveloperSigningCredential()             
                .AddClientStore<ClientStoreWrapper>()
                
                .AddInMemoryApiResources(new List<ApiResource>
                {
                    new ApiResource("identity:apps:read:all"),
                    new ApiResource("compute_api")
                })
                .AddInMemoryCaching()

                .AddInMemoryIdentityResources(
                    new[]
                    {
                        new IdentityResources.OpenId(),
                    });

            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            });


            //JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            //JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            //services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            //    .AddJwtBearer(options =>
            //    {
            //        options.Authority = "https://localhost:62189/identity";
            //        options.Audience = "identity_api";
            //        options.RequireHttpsMetadata = false;
            //    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:apps:read:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:read:all", "http://localhost:62189/identity")));
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:apps:write:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:write:all", "http://localhost:62189/identity")));
            });

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();
        }

        public override void Configure(IApplicationBuilder app)
        {

            app.UseIdentityServer();

            app.UseAuthentication();

            app.UseMvcWithDefaultRoute();


        }

    }
}