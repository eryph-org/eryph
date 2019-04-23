using System;
using Haipa.IdentityDb;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Haipa.Modules.Identity
{
    public class IdentityModule : WebModuleBase
    {
        public override string Name => "Haipa.Identity";
        public override string Path => "identity";


        protected override void ConfigureServices(IServiceProvider serviceProvider, IServiceCollection services)
        {
            services.AddScopedModuleHandler<IdentityInitializer>();

            services.AddMvc(op => { op.EnableEndpointRouting = false; })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddApplicationPart(typeof(IdentityModule).Assembly);


            services.AddDbContext<IdentityDbContext>(options =>
            {
                serviceProvider.GetService<IDbContextConfigurer<IdentityDbContext>>().Configure(options);

                // Register the entity sets needed by OpenIddict.
                // Note: use the generic overload if you need
                // to replace the default OpenIddict entities.
                options.UseOpenIddict();
            });

            services.AddOpenIddict()

                // Register the OpenIddict core services.
                .AddCore(options =>
                {
                    // Register the Entity Framework stores and models.
                    options.UseEntityFrameworkCore()
                        .UseDbContext<IdentityDbContext>();
                })

                // Register the OpenIddict server handler.
                .AddServer(options =>
                {
                    // Register the ASP.NET Core MVC binder used by OpenIddict.
                    // Note: if you don't call this method, you won't be able to
                    // bind OpenIdConnectRequest or OpenIdConnectResponse parameters.
                    options.UseMvc();

                    // Enable the token endpoint.
                    options.EnableTokenEndpoint("/connect/token");

                    // Enable the client credentials flow.
                    options.AllowClientCredentialsFlow();

                    // During development, you can disable the HTTPS requirement.
                    options.DisableHttpsRequirement();

                    // Note: to use JWT access tokens instead of the default
                    // encrypted format, the following lines are required:
                    //
                    options.UseJsonWebTokens();
                    options.AddEphemeralSigningKey();
                });

            services.AddTenant();
        }

        protected override void Configure(IApplicationBuilder app)
        {
            app.UseTenant();

            app.UseAuthentication();
            app.UseMvc(b =>
            {

            });

        }

    }
}
