using System;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using AspNet.Security.OpenIdConnect.Primitives;
using AuthorizationServer.Services;
using Haipa.IdentityDb;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.EntityFrameworkCore;
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
            services.AddScopedModuleHandler<IdentityInitializer>();

            services.AddMvc(op => { op.EnableEndpointRouting = true; })

                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)

                .ConfigureApplicationPartManager(apm =>
                {
                    var razorAssemblyPath =
                        System.IO.Path.ChangeExtension(typeof(IdentityModule).Assembly.Location, ".views.dll");
                    var razorAssembly = Assembly.LoadFile(razorAssemblyPath);

                    apm.ApplicationParts.Add(new AssemblyPart(typeof(IdentityModule).Assembly));
                    apm.ApplicationParts.Add(new CompiledRazorAssemblyPart(razorAssembly));

                });


            services.AddDbContext<IdentityDbContext>(options =>
            {
                serviceProvider.GetService<IDbContextConfigurer<IdentityDbContext>>().Configure(options);

                // Register the entity sets needed by OpenIddict.
                // Note: use the generic overload if you need
                // to replace the default OpenIddict entities.
                options.UseOpenIddict();
            });

            // Register the Identity services.

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<IdentityDbContext>()
                .AddDefaultTokenProviders();

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

                    // Enable the authorization, logout, token and userinfo endpoints.
                    options.EnableAuthorizationEndpoint("/connect/authorize")
                        .EnableLogoutEndpoint("/connect/logout")
                        .EnableTokenEndpoint("/connect/token")
                        .EnableUserinfoEndpoint("/api/userinfo");

                    // Enable the client credentials flow.
                    //options.AllowClientCredentialsFlow();
                    options.AllowAuthorizationCodeFlow();

                    // When request caching is enabled, authorization and logout requests
                    // are stored in the distributed cache by OpenIddict and the user agent
                    // is redirected to the same page with a single parameter (request_id).
                    // This allows flowing large OpenID Connect requests even when using
                    // an external authentication provider like Google, Facebook or Twitter.
                    options.EnableRequestCaching();

                    // During development, you can disable the HTTPS requirement.
                    options.DisableHttpsRequirement();

                    // Note: to use JWT access tokens instead of the default
                    // encrypted format, the following lines are required:
                    //
                    options.UseJsonWebTokens();
                    options.AddEphemeralSigningKey();

                    options.RegisterScopes(
                        "identity:apps:read:all",
                        "identity:apps:write:all"
                    );
                });

            services.Configure<IdentityOptions>(options =>
            {
                options.ClaimsIdentity.UserNameClaimType = OpenIdConnectConstants.Claims.Name;
                options.ClaimsIdentity.UserIdClaimType = OpenIdConnectConstants.Claims.Subject;
                options.ClaimsIdentity.RoleClaimType = OpenIdConnectConstants.Claims.Role;
            });


            services.AddTenant();

            services.AddApiVersioning(options =>
            {
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = false;
            });


            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.Clear();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = "https://localhost:62189/identity";
                    options.Audience = "identity_api";
                    options.RequireHttpsMetadata = false;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:apps:read:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:read:all", "http://localhost:62189/identity")));
            });
            services.AddAuthorization(options =>
            {
                options.AddPolicy("identity:apps:write:all", policy => policy.Requirements.Add(new HasScopeRequirement("identity:apps:write:all", "http://localhost:62189/identity")));
            });

            services.AddSingleton<IAuthorizationHandler, HasScopeHandler>();


            services.AddTransient<IEmailSender, AuthMessageSender>();

            services.AddTransient<ISmsSender, AuthMessageSender>();
        }

        protected override void Configure(IApplicationBuilder app)
        {
            app.UseTenant();

            app.UseStaticFiles();


            app.UseAuthentication();

            app.UseMvcWithDefaultRoute();


        }

    }
}
